using AIEnablement.Core.Interfaces.EvaluationQualityMetrics;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using AIEnablement.Core.Models.EvaluationQualityMetrics;
using AIEnablement.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using System.Text.Json;

namespace AIEnablement.Infrastructure.Services.EvaluationQualityMetrics;

public class MetricsService : IMetricsService
{
    private readonly Kernel _kernel;
    private readonly IPromptService _promptService;
    private readonly EvaluationConfiguration _config;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(
        IPromptService promptService,
        EvaluationConfiguration config,
        RagChatbotConfiguration ragConfig,
        ILogger<MetricsService> logger)
    {
        _config = config;
        _logger = logger;
        _promptService = promptService;

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: config.Metrics.EvaluatorModel,
            apiKey: config.ApiKeys.OpenAI,
            endpoint: new Uri(ragConfig.ApiEndpoint.Endpoint)
        );
        _kernel = builder.Build();
    }

    public async Task<MetricScores> EvaluateAsync(ModelResponse response, GoldenDataItem goldenData)
    {
        _logger.LogInformation("Evaluating response for query: {Query}", goldenData.Query);

        var scores = new MetricScores();

        // Retrieval Metrics
        var retrievalMetrics = await RetrievalMetricsEvaluatorAsync(response, goldenData);
        scores.ContextualPrecision = retrievalMetrics.ContextualPrecision.Score;
        scores.ContextUtilizationReason = retrievalMetrics.ContextualPrecision.Reason;

        scores.ContextualRecall = retrievalMetrics.ContextualRecall.Score;
        scores.ContextualRecallReason = retrievalMetrics.ContextualRecall.Reason;

        scores.ContextUtilization = retrievalMetrics.ContextUtilization.Score;
        scores.ContextUtilizationReason = retrievalMetrics.ContextUtilization.Reason;

        // Error Analysis
        scores.FalseNegatives = retrievalMetrics.RetrievalErrors.FalseNegatives;
        scores.FalsePositives = retrievalMetrics.RetrievalErrors.FalsePositives;

        // Answer Quality Metrics
        var answerQuality = await AnswerQualityEvaluatorAsync(response, goldenData);
        scores.AnswerRelevancy = answerQuality.AnswerRelevancy.Score;
        scores.AnswerRelevancyReason = answerQuality.AnswerRelevancy.Reason;

        scores.Faithfulness = answerQuality.Faithfulness.Score;
        scores.FaithfulnessReason = answerQuality.Faithfulness.Reason;

        scores.AnswerCorrectness = answerQuality.AnswerCorrectness.Score;
        scores.AnswerCorrectnessReason = answerQuality.AnswerCorrectness.Reason;

        return scores;
    }

    private async Task<RetrievalMetrics> RetrievalMetricsEvaluatorAsync(ModelResponse response, GoldenDataItem goldenData)
    {
        // Contextual Precision: How many retrieved chunks are actually relevant?
        // Contextual Recall: Are all important pieces of information retrieved?
        // Context Utilization: How much of the retrieved context is actually used in the answer?
        // Analyze retrieval errors for this query.

        var (toolExecutions, sources) = ExtractChunksFromTools(response);

        var promptData = _promptService.LoadPromptFromFile(_config.EvaluationPromptPaths.RetrievalMetricsPromptPath);
        var systemPromptTemplate = promptData["system_prompt"];
        var userPromptTemplate = promptData["user_prompt"];

        var userPrompt = userPromptTemplate
            .Replace("{golden_data_query}", goldenData.Query)
            .Replace("{expected_sources}", string.Join(", ", goldenData.ExpectedSources))
            .Replace("{required_facts}", string.Join(", ", goldenData.RequiredFacts))
            .Replace("{tool_executions}", toolExecutions)
            .Replace("{sources}", sources)
            .Replace("{response_answer}", response.Answer)
            .Replace("{reference_answer}", goldenData.ReferenceAnswer);

        var result = await CallEvaluatorAsync<RetrievalMetrics>(systemPromptTemplate, userPrompt);

        return result;
    }

    private async Task<AnswerQuality> AnswerQualityEvaluatorAsync(ModelResponse response, GoldenDataItem goldenData)
    {
        // Answer Relevancy: Does the answer address the query?
        // Faithfulness: Is the answer grounded in the retrieved context? No hallucinations?
        // Answer Correctness: How correct is the answer compared to reference?

        var (toolExecutions, _) = ExtractChunksFromTools(response);

        var promptData = _promptService.LoadPromptFromFile(_config.EvaluationPromptPaths.AnswerQualityPromptPath);
        var systemPromptTemplate = promptData["system_prompt"];
        var userPromptTemplate = promptData["user_prompt"];

        var userPrompt = userPromptTemplate
            .Replace("{golden_data_query}", goldenData.Query)
            .Replace("{required_facts}", string.Join(", ", goldenData.RequiredFacts))
            .Replace("{reference_answer}", goldenData.ReferenceAnswer)
            .Replace("{tool_executions}", toolExecutions)
            .Replace("{response_answer}", response.Answer);

        var result = await CallEvaluatorAsync<AnswerQuality>(systemPromptTemplate, userPrompt);

        return result;
    }

    private async Task<T> CallEvaluatorAsync<T>(string systemMessage, string prompt) where T : class
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(systemMessage);

        chatHistory.AddUserMessage(prompt);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.Metrics.EvaluatorTemperature,
            TopP = _config.Metrics.EvaluatorTopP,
            MaxTokens = 1000,
            ResponseFormat = typeof(T)
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            settings,
            _kernel
        );

        var content = result.Content ?? throw new InvalidOperationException("LLM returned null content");

        return JsonSerializer.Deserialize<T>(content) ?? throw new InvalidOperationException("Failed to deserialize structured output");
    }

    private static (string toolExecutions, string sources) ExtractChunksFromTools(ModelResponse response)
    {
        const string searchDocumentToolName = "search_documents";

        var toolContents = response.ToolExecutions.Where(tool => !string.IsNullOrWhiteSpace(tool.Content) && tool.ToolName == searchDocumentToolName).ToArray();
        var documentChunks = new List<RagDocumentChunks>();
        foreach (var toolContent in toolContents)
        {
            var ragResult = JsonSerializer.Deserialize<RagResult>(toolContent.Content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });

            documentChunks.AddRange(ragResult.Results);
        }

        var toolExecutions = string.Empty;
        var sources = string.Empty;

        if (documentChunks.Any())
        {
            var stringBuilder = new StringBuilder();
            var sourcesStringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Tool: {searchDocumentToolName}");

            foreach (var chunk in documentChunks)
            {
                stringBuilder.AppendLine($"Chunk number: {chunk.ResultNumber}");

                stringBuilder.AppendLine("Chunk:");
                stringBuilder.AppendLine(chunk.Content);

                if (chunk.NeighborContext is not null)
                {
                    foreach (var chunkBefore in chunk.NeighborContext.ChunksBefore)
                    {
                        stringBuilder.AppendLine("Chunk before:");
                        stringBuilder.AppendLine(chunkBefore);
                    }

                    foreach (var chunkAfter in chunk.NeighborContext.ChunksAfter)
                    {
                        stringBuilder.AppendLine("Chunk after:");
                        stringBuilder.AppendLine(chunkAfter);
                    }
                }

                stringBuilder.AppendLine($"Relevance score: {chunk.RelevanceScore.ToString()}");

                stringBuilder.AppendLine("Source:");
                stringBuilder.AppendLine($"section_id: {chunk.Source.SectionId}");
                stringBuilder.AppendLine($"document_tool_name: {chunk.Source.ToolName}");
                stringBuilder.AppendLine($"document_type: {chunk.Source.DocumentType}");
                stringBuilder.AppendLine($"section_heading: {chunk.Source.SectionHeading}");
                stringBuilder.AppendLine($"file_path: {chunk.Source.FilePath}");

                stringBuilder.AppendLine();

                sourcesStringBuilder.AppendLine($"{chunk.Source.FilePath}#{chunk.Source.SectionId}");
            }

            toolExecutions = stringBuilder.ToString();
            sources = sourcesStringBuilder.ToString();
        }
        else
        {
            var stringBuilder = new StringBuilder();
            var chunkNumber = 1;
            var tools = response.ToolExecutions.Where(tool => !string.IsNullOrWhiteSpace(tool.Content)).ToArray();
            foreach (var tool in tools)
            {
                stringBuilder.AppendLine($"Tool: {tool.ToolName}");

                stringBuilder.AppendLine($"Chunk number: {chunkNumber}");
                stringBuilder.AppendLine("Chunk (Tool response):");
                stringBuilder.AppendLine(tool.Content);

                stringBuilder.AppendLine();

                chunkNumber++;
            }

            toolExecutions = stringBuilder.ToString();
        }

        return (toolExecutions, sources);
    }
}
