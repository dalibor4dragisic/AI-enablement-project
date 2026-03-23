using AIEnablement.Core.Interfaces.AgenticRAGAssistant;
using AIEnablement.Core.Interfaces.EvaluationQualityMetrics;
using AIEnablement.Core.Models.EvaluationQualityMetrics;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;

namespace AIEnablement.Business.Services.EvaluationQualityMetrics;

public class ExperimentRunner : IExperimentRunner
{
    private readonly IAgenticRAGChatService _chatService;
    private readonly IMetricsService _metricsService;
    private readonly EvaluationConfiguration _config;
    private readonly ILogger<ExperimentRunner> _logger;

    public ExperimentRunner(
        IAgenticRAGChatService chatService,
        IMetricsService metricsService,
        EvaluationConfiguration config,
        ILogger<ExperimentRunner> logger)
    {
        _chatService = chatService;
        _metricsService = metricsService;
        _config = config;
        _logger = logger;
    }

    public async Task<ExperimentResults> RunExperimentsAsync(GoldenDataset dataset)
    {
        _logger.LogInformation("Starting experiments with {Count} configurations",
            _config.Experiments.Configurations.Count);

        var experimentResults = new ExperimentResults
        {
            ExperimentName = $"RAG_Evaluation_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
            RunAt = DateTime.UtcNow,
            ConfigurationResults = new List<ConfigurationResults>()
        };

        foreach (var configDef in _config.Experiments.Configurations)
        {
            var ragConfig = new ModelConfiguration
            {
                Name = configDef.Name,
                Description = configDef.Description,
                ModelId = configDef.ModelId,
                Temperature = configDef.Temperature,
                TopP = configDef.TopP,
                TopK = configDef.TopK,
                MinRelevanceScore = configDef.MinRelevanceScore,
                IncludeNeighbors = configDef.IncludeNeighbors
            };

            _logger.LogInformation("Running experiment: {Config}", configDef.Name);

            var configResults = await RunSingleConfigurationAsync(ragConfig, dataset);
            experimentResults.ConfigurationResults.Add(configResults);

            // Save intermediate results
            //await SaveIntermediateResultsAsync(experimentResults);
        }

        // Save final results
        await SaveExperimentResultsAsync(experimentResults);

        _logger.LogInformation("Experiments complete!");
        return experimentResults;
    }

    private async Task<ConfigurationResults> RunSingleConfigurationAsync(
        ModelConfiguration config,
        GoldenDataset dataset)
    {
        _logger.LogInformation("Testing configuration: {Name} - {Description}",
            config.Name, config.Description);

        var results = new List<EvaluationResult>();
        var totalResponseTime = TimeSpan.Zero;

        int completed = 0;
        foreach (var goldenItem in dataset.Items)
        {
            completed++;
            _logger.LogInformation("[{Config}] Query {N}/{Total}: {Query}",
                config.Name, completed, dataset.Items.Count, goldenItem.Query);

            try
            {
                // Query RAG system
                var stopwatch = Stopwatch.StartNew();
                var ragResponse = await _chatService.GetResponseAsync(goldenItem.Query, config);
                stopwatch.Stop();

                totalResponseTime += stopwatch.Elapsed;

                // Evaluate response
                var metrics = await _metricsService.EvaluateAsync(ragResponse, goldenItem);

                var evaluationResult = new EvaluationResult
                {
                    QueryId = goldenItem.Id,
                    ConfigurationName = config.Name,
                    RagResponse = ragResponse,
                    GoldenData = goldenItem,
                    Metrics = metrics,
                    EvaluatedAt = DateTime.UtcNow
                };

                results.Add(evaluationResult);

                _logger.LogInformation("  Scores: P={P:F3} R={R:F3} U={U:F3} | AR={AR:F3} F={F:F3} AC={AC:F3}",
                    metrics.ContextualPrecision,
                    metrics.ContextualRecall,
                    metrics.ContextUtilization,
                    metrics.AnswerRelevancy,
                    metrics.Faithfulness,
                    metrics.AnswerCorrectness);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating query: {Query}", goldenItem.Query);
            }
        }

        // Calculate aggregate metrics
        var aggregateMetrics = CalculateAggregateMetrics(results);

        var performanceMetrics = new PerformanceMetrics
        {
            AvgResponseTime = TimeSpan.FromMilliseconds(totalResponseTime.TotalMilliseconds / results.Count),
        };

        _logger.LogInformation("Configuration {Name} complete. Avg scores: P={P:F3} R={R:F3} U={U:F3}",
            config.Name,
            aggregateMetrics.AvgContextualPrecision,
            aggregateMetrics.AvgContextualRecall,
            aggregateMetrics.AvgContextUtilization);

        return new ConfigurationResults
        {
            Configuration = config,
            Results = results,
            AggregateMetrics = aggregateMetrics,
            Performance = performanceMetrics
        };
    }

    private AggregateMetrics CalculateAggregateMetrics(List<EvaluationResult> results)
    {
        if (!results.Any())
            return new AggregateMetrics();

        return new AggregateMetrics
        {
            AvgContextualPrecision = results.Average(r => r.Metrics.ContextualPrecision),
            AvgContextualRecall = results.Average(r => r.Metrics.ContextualRecall),
            AvgContextUtilization = results.Average(r => r.Metrics.ContextUtilization),
            AvgAnswerRelevancy = results.Average(r => r.Metrics.AnswerRelevancy),
            AvgFaithfulness = results.Average(r => r.Metrics.Faithfulness),
            AvgAnswerCorrectness = results.Average(r => r.Metrics.AnswerCorrectness),
            TotalFalsePositives = results.Sum(r => r.Metrics.FalsePositives.Count),
            TotalFalseNegatives = results.Sum(r => r.Metrics.FalseNegatives.Count)
        };
    }

    private async Task SaveExperimentResultsAsync(ExperimentResults results)
    {
        var outputDir = _config.Experiments.OutputDirectory;
        Directory.CreateDirectory(outputDir);

        var filename = $"{results.ExperimentName}.json";
        var path = Path.Combine(outputDir, filename);

        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(path, json);
        _logger.LogInformation("Saved experiment results to {Path}", path);
    }

    //private async Task SaveIntermediateResultsAsync(ExperimentResults results)
    //{
    //    var outputDir = _config.Experiments.OutputDirectory;
    //    Directory.CreateDirectory(outputDir);

    //    var filename = $"{results.ExperimentName}_intermediate.json";
    //    var path = Path.Combine(outputDir, filename);

    //    var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
    //    {
    //        WriteIndented = true
    //    });

    //    await File.WriteAllTextAsync(path, json);
    //}
}
