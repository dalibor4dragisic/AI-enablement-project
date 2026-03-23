using AIEnablement.Core.Interfaces.AgenticRAGAssistant;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using AIEnablement.Core.Models.Chatbot;
using AIEnablement.Core.Models.EvaluationQualityMetrics;
using AIEnablement.Infrastructure.Plugins;
using AIEnablement.Infrastructure.Plugins.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AIEnablement.Infrastructure.Services.AgenticRAGAssistant;

public class AgenticRAGChatService : IAgenticRAGChatService
{
    private readonly Kernel _kernel;
    private readonly RagChatbotConfiguration _config;
    private readonly ILogger<LLMChatService> _logger;
    private readonly IPromptService _promptService;
    private readonly Tokenizer _tokenizer;
    private readonly ChatHistory _chatHistory = new();
    private readonly Dictionary<ChatMessageContent, DateTime> _chatMessagesDates = new();
    private readonly DateTime _conversationStarted;

    public AgenticRAGChatService(
        RagChatbotConfiguration config,
        DateToolsPlugin dateToolsPlugin,
        RetrievalToolPlugin retrievalToolPlugin,
        ILogger<LLMChatService> logger,
        IPromptService promptService)
    {
        _config = config;
        _logger = logger;
        _promptService = promptService;
        _tokenizer = TiktokenTokenizer.CreateForModel(config.Chunking.TokenizerModel);

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: config.Model.ModelId,
            endpoint: new Uri(_config.ApiEndpoint.Endpoint),
            apiKey: config.ApiKeys.OpenAI,
            serviceId: config.Model.ModelId
        );

        builder.AddOpenAIChatCompletion(
            modelId: config.Model.ModelLightId,
            endpoint: new Uri(_config.ApiEndpoint.Endpoint),
            apiKey: config.ApiKeys.OpenAI,
            serviceId: config.Model.ModelLightId
        );

        if (config.Assistant.EnableTools && config.Tools["DateTools"].Enabled)
        {
            builder.Plugins.AddFromObject(dateToolsPlugin, "DateTools");
        }

        if (config.Assistant.EnableTools && config.Tools["RetrievalTool"].Enabled)
        {
            builder.Plugins.AddFromObject(retrievalToolPlugin, "RetrievalTool");
        }

        _kernel = builder.Build();

        LoadSystemPrompt();

        _conversationStarted = DateTime.Now;
    }

    public async Task<string> GetResponseAsync(string message)
    {
        await CheckAndSummarizeIfNeededAsync();

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>(_config.Model.ModelId);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.Model.Temperature,
            MaxTokens = _config.Model.MaxTokens,
            TopP = _config.Model.TopP,
            ToolCallBehavior = _config.Assistant.EnableTools
                ? ToolCallBehavior.AutoInvokeKernelFunctions
                : null
        };

        try
        {
            _chatHistory.AddUserMessage(message);

            RefreshMessageTimeStamps();

            var result = await chatCompletion.GetChatMessageContentAsync(
                _chatHistory,
                executionSettings,
                _kernel
            );

            _chatHistory.Add(result);

            RefreshMessageTimeStamps();

            return result.Content ?? throw new InvalidOperationException("LLM returned null content");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            throw;
        }
    }

    public async Task<ModelResponse> GetResponseAsync(string message, ModelConfiguration configuration)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>(configuration.ModelId);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = configuration.Temperature,
            MaxTokens = _config.Model.MaxTokens,
            TopP = configuration.TopP,
            ToolCallBehavior = _config.Assistant.EnableTools
                ? ToolCallBehavior.AutoInvokeKernelFunctions
                : null
        };

        try
        {
            var history = new ChatHistory();

            var systemPrompt = GetSystemPrompt();
            history.AddSystemMessage(systemPrompt);

            var question = new ChatMessageContent(AuthorRole.User, message);
            history.Add(question);

            _kernel.AutoFunctionInvocationFilters.Add(new ManualParamsRetrievalFilter(configuration.TopK, configuration.IncludeNeighbors, configuration.MinRelevanceScore));

            var result = await chatCompletion.GetChatMessageContentAsync(
                history,
                executionSettings,
                _kernel
            );

            if (result.Content is null)
            {
                throw new InvalidOperationException("LLM returned null content");
            }

            history.Add(result);

            var toolExecutions = GetToolExecutions(question, result, history);
            var modelResponse = new ModelResponse
            {
                Query = message,
                Answer = result.Content,
                Configuration = configuration,
                ToolExecutions = toolExecutions
            };

            return modelResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            throw;
        }
    }

    public async Task<ConversationSummary> GetSummary()
    {
        const string getCurrentDate = "get_current_date";
        const string addDaysToDate = "add_days_to_date";
        const string searchDocuments = "search_documents";
        var assistantMessages = _chatHistory.Where(chatContent => chatContent.Role == AuthorRole.Assistant).ToList();
        var userMessages = _chatHistory.Count(chatContent => chatContent.Role == AuthorRole.User);
        var assistantMessagesCount = assistantMessages.Count();
        var systemMessages = _chatHistory.Count(chatContent => chatContent.Role == AuthorRole.System);

        var getCurrentDateToolCalls = 0;
        var addDaysToDateToolCalls = 0;
        var searchDocumentsToolCalls = 0;

        foreach (var message in _chatHistory.Where(historyItem => historyItem.Role == AuthorRole.Tool))
        {
            foreach (var item in message.Items)
            {
                if (item is FunctionResultContent functionResultContent)
                {
                    var functionName = functionResultContent.FunctionName;

                    if (functionName == getCurrentDate)
                    {
                        getCurrentDateToolCalls++;
                    }
                    else if (functionName == addDaysToDate)
                    {
                        addDaysToDateToolCalls++;
                    }
                    else if (functionName == searchDocuments)
                    {
                        searchDocumentsToolCalls++;
                    }
                }
            }
        }

        var summaryMessage = await GenerateConversationSummaryAsync();

        return new ConversationSummary
        {
            TotalMessages = _chatHistory.Count,
            UserMessages = userMessages,
            SystemMessages = systemMessages,
            AssistantMessages = assistantMessagesCount,
            GetCurrentDateToolCalls = getCurrentDateToolCalls,
            AddDaysToDateToolCalls = addDaysToDateToolCalls,
            SearchDocumentsToolCalls = searchDocumentsToolCalls,
            ConversationStarted = _conversationStarted,
            Duration = DateTime.Now - _conversationStarted,
            SummaryText = summaryMessage,
            EstimatedTokens = EstimateTokenCount()
        };
    }

    public IReadOnlyCollection<ChatContent> GetAllMessages()
    {
        var messages = GetAllMessages(_chatHistory);

        return messages;
    }

    private async Task<string> GenerateConversationSummaryAsync()
    {
        var conversationHistory = _chatHistory
            .Where(m => m.Role != AuthorRole.System)
            .ToArray();

        if (!conversationHistory.Any())
        {
            return string.Empty;
        }

        var result = await GetConversationSummaryPreserveAsync(_chatHistory.ToArray());

        return result.Content ?? string.Empty;
    }

    private async Task<ChatMessageContent> GetConversationSummaryPreserveAsync(ChatMessageContent[] conversationHistory)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>(_config.Model.ModelLightId);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.Model.Temperature,
            MaxTokens = _config.Model.MaxTokens,
            TopP = _config.Model.TopP
        };

        var promptData = _promptService.LoadPromptFromFile(_config.Assistant.PersonalityPromptPath);
        var userPromptTemplate = promptData["user_prompt"];

        var chatHistory = new ChatHistory();
        chatHistory.AddRange(conversationHistory);
        chatHistory.AddUserMessage(userPromptTemplate);
        
        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel
        );

        return result ?? throw new InvalidOperationException("LLM returned null content");
    }

    private Dictionary<int, List<ChatMessageContent>> GetGroupedMessages(IReadOnlyCollection<ChatMessageContent> chatMessages)
    {
        var messagesGroup = new Dictionary<int, List<ChatMessageContent>>();
        var groupCreated = false;
        var groupNumber = 0;
        foreach (var chatMessage in chatMessages)
        {
            if (chatMessage.Role == AuthorRole.System)
            {
                continue;
            }

            if (chatMessage.Role == AuthorRole.User 
                && !string.IsNullOrWhiteSpace(chatMessage.Content))
            {
                groupCreated = true;

                messagesGroup.TryAdd(groupNumber, [chatMessage]);
            }
            else if (groupCreated)
            {
                messagesGroup[groupNumber].Add(chatMessage);

                if (chatMessage.Role == AuthorRole.Assistant
                    && !string.IsNullOrWhiteSpace(chatMessage.Content))
                {
                    groupCreated = false;
                    groupNumber++;
                }
            }
        }

        return messagesGroup;
    }

    private string GetToolCallId(ChatMessageContent chatMessageContent)
    {
        if (chatMessageContent.Role != AuthorRole.Tool)
        {
            return string.Empty;
        }

        var toolIds = new List<string>();

        foreach (var item in chatMessageContent.Items)
        {
            if (item is FunctionResultContent functionCallContent)
            {
                if (!string.IsNullOrWhiteSpace(functionCallContent.CallId))
                {
                    toolIds.Add(functionCallContent.CallId);
                }
            }
        }

        return string.Join(", ", toolIds);
    }

    private string GetToolName(ChatMessageContent chatMessageContent)
    {
        if (chatMessageContent.Role != AuthorRole.Tool)
        {
            return string.Empty;
        }

        var toolNames = new List<string>();

        foreach (var item in chatMessageContent.Items)
        {
            if (item is FunctionResultContent functionCallContent)
            {
                if (!string.IsNullOrWhiteSpace(functionCallContent.FunctionName))
                {
                    toolNames.Add(functionCallContent.FunctionName);
                }
            }
        }

        return string.Join(", ", toolNames);
    }

    private IReadOnlyCollection<ChatContent> GetAllMessages(IReadOnlyCollection<ChatMessageContent> chatHistory)
    {
        var messages = new List<ChatContent>();

        foreach (var chatHistoryItem in chatHistory)
        {
            _chatMessagesDates.TryGetValue(chatHistoryItem, out var dateTIme);

            if (chatHistoryItem.Role == AuthorRole.Assistant)
            {
                var chatContent = new ChatContent
                {
                    Role = ChatRole.Assistant,
                    Timestamp = dateTIme
                };

                if (chatHistoryItem is OpenAIChatMessageContent openAIChatMessageContent)
                {
                    if (openAIChatMessageContent.ToolCalls.Any())
                    {
                        var toolCallContents = openAIChatMessageContent.ToolCalls.Select(toolCall =>
                        {
                            var functionName = toolCall.FunctionName;
                            var arguments = toolCall.FunctionArguments.ToString();

                            return $"[Called function: {functionName.Replace("DateTools-", string.Empty)
                                .Replace("RetrievalTool-", string.Empty)}, arguments: {arguments}, id: {toolCall.Id}]";
                        }).ToArray();

                        chatContent.Content = string.Join("; ", toolCallContents);
                    }
                    else
                    {
                        chatContent.Content = chatHistoryItem.Content ?? string.Empty;
                    }

                    messages.Add(chatContent);
                }
                else
                {
                    chatContent.Content = chatHistoryItem.Content ?? string.Empty;
                    messages.Add(chatContent);
                }
            }
            else if (chatHistoryItem.Role == AuthorRole.User)
            {
                messages.Add(new ChatContent
                {
                    Content = chatHistoryItem.Content ?? string.Empty,
                    Role = ChatRole.User,
                    Timestamp = dateTIme
                });
            }
            else if (chatHistoryItem.Role == AuthorRole.Tool)
            {
                var toolId = GetToolCallId(chatHistoryItem);

                messages.Add(new ChatContent
                {
                    Content = chatHistoryItem.Content != null ? $"Returned: {chatHistoryItem.Content}, {toolId}" : toolId,
                    Role = ChatRole.Tool,
                    Timestamp = dateTIme
                });
            }
        }

        return messages;
    }

    private void LoadSystemPrompt()
    {
        var systemPrompt = GetSystemPrompt();

        _chatHistory.AddSystemMessage(systemPrompt);

        var systemMessage = _chatHistory.FirstOrDefault(chat => chat.Role == AuthorRole.System);
        if (systemMessage != null)
        {
            _chatMessagesDates.TryAdd(systemMessage, DateTime.Now);
        }
    }

    private string GetSystemPrompt()
    {
        var data = _promptService.LoadPromptFromFile(_config.Assistant.PersonalityPromptPath);

        var systemPromptTemplate = data["system_prompt"];
        var systemPrompt = systemPromptTemplate.Replace("{assistant_name}", _config.Assistant.Name)
            .Replace("{previous_summary_text}", string.Empty);

        return systemPrompt;
    }

    private void RefreshMessageTimeStamps()
    {
        var messages = _chatHistory.Where(chat => !_chatMessagesDates.Keys.Contains(chat));
        foreach (var message in messages)
        {
            _chatMessagesDates.Add(message, DateTime.Now);
        }

        var messageDatesForRemove = _chatMessagesDates.Keys.Where(key => !_chatHistory.Contains(key)).ToArray();
        foreach (var messageDateForRemove in messageDatesForRemove)
        {
            _chatMessagesDates.Remove(messageDateForRemove);
        }
    }

    public async Task CheckAndSummarizeIfNeededAsync()
    {
        var currentTokens = EstimateTokenCount();
        var maxTokens = _config.Model.MaxInputTokens;
        var threshold = maxTokens * _config.Assistant.SummarizationThreshold;

        //_logger.LogDebug("Current tokens: {Current}/{Max} (threshold: {Threshold})",
        //    currentTokens, maxTokens, threshold);

        if (currentTokens < threshold)
        {
            return;
        }

        //_logger.LogInformation("Token count ({Current}) exceeds threshold ({Threshold}), performing summarization",
        //    currentTokens, threshold);

        await SummarizeAndCompactAsync();
    }


    private async Task SummarizeAndCompactAsync()
    {
        //Get all non-system messages
        var groupedMessages = GetGroupedMessages(_chatHistory);

        if (groupedMessages.Count <= _config.Assistant.KeepRecentCount)
        {
            _logger.LogWarning("Not enough messages to summarize");
            return;
        }

        var oldMessages = groupedMessages.Take(groupedMessages.Count - _config.Assistant.KeepRecentCount).SelectMany(pair => pair.Value).ToList();
        var recentMessages = groupedMessages.Skip(groupedMessages.Count - _config.Assistant.KeepRecentCount).SelectMany(pair => pair.Value).ToList();

        //_logger.LogInformation("Summarizing {Old} messages, keeping {Recent} recent messages",
        //    oldMessages.Count, recentMessages.Count);

        try
        {
            var systemMessage = _chatHistory.First(m => m.Role == AuthorRole.System);

            //Generate summary
            var summary = await GetConversationSummaryPreserveAsync(new[] { systemMessage }.Concat(oldMessages).ToArray());
            
            //Rebuild chat history
            _chatHistory.Clear();

            //Add original system message
            var promptData = _promptService.LoadPromptFromFile(_config.Assistant.PersonalityPromptPath);
            var systemPromptTemplate = promptData["system_prompt"];
            var systemPrompt = systemPromptTemplate.Replace("{previous_summary_text}", summary.Content)
                .Replace("{assistant_name}", _config.Assistant.Name);
            systemMessage.Content = systemPrompt;

            _chatHistory.Add(systemMessage);

            _chatHistory.AddRange(recentMessages);

            RefreshMessageTimeStamps();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize conversation");
        }
    }

    public int EstimateTokenCount()
    {
        int total = 0;

        foreach (var message in _chatHistory)
        {
            var content = message.Content ?? string.Empty;
            var tokens = _tokenizer.EncodeToTokens(content, out _);
            total += tokens.Count;

            //Add tokens for tool calls/results
            if (message.Items?.Any() == true)
            {
                foreach (var item in message.Items)
                {
                    var itemText = item.ToString() ?? "";
                    var itemTokens = _tokenizer.EncodeToTokens(itemText, out _);
                    total += itemTokens.Count;
                }
            }
        }

        return total;
    }

    private List<ToolExecution> GetToolExecutions(ChatMessageContent question, ChatMessageContent answer, ChatHistory chatHistory)
    {
        var toolExecutions = new List<ToolExecution>();

        var startIndex = chatHistory.IndexOf(question);
        var endIndex = chatHistory.IndexOf(answer);

        if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
        {
            // Skip the start element and everything before it, then skip the start element itself.
            // Take elements for the calculated count.
            var assistantAndToolMessages = chatHistory.Skip(startIndex + 1).Take(endIndex - startIndex - 1).ToList();

            foreach (var message in assistantAndToolMessages)
            {
                if (message.Role == AuthorRole.Assistant)
                {
                    if (message is OpenAIChatMessageContent openAIChatMessageContent)
                    {
                        if (openAIChatMessageContent.ToolCalls.Any())
                        {
                            var toolCallContents = openAIChatMessageContent.ToolCalls.Select(toolCall =>
                            {
                                var functionName = toolCall.FunctionName;
                                var arguments = toolCall.FunctionArguments.ToString();

                                return new ToolExecution
                                {
                                    Role = AuthorRole.Assistant.ToString(),
                                    ToolCall = new ToolCall
                                    {
                                        ToolName = functionName.Replace("DateTools-", string.Empty).Replace("RetrievalTool-", string.Empty),
                                        Arguments = arguments
                                    }
                                };
                            }).ToArray();

                            toolExecutions.AddRange(toolCallContents);
                        }
                    }
                }
                else if (message.Role == AuthorRole.Tool)
                {
                    var toolName = GetToolName(message);

                    toolExecutions.Add(new ToolExecution
                    {
                        Role = AuthorRole.Tool.ToString(),
                        ToolName = toolName,
                        Content = message.Content,
                    });
                }
            }
        }

        return toolExecutions;
    }

    public GoldenDataset CreateGoldenDatasetFromHistory()
    {
        var goldenDataset = new GoldenDataset
        {
            CreatedAt = DateTime.Now,
            Validated = false
        };

        var chatHistory = GetGroupedMessages(_chatHistory);

        foreach (var historyGroup in chatHistory)
        {
            var userQuestion = historyGroup.Value.First(item => item.Role == AuthorRole.User);
            var assistantAnswer = historyGroup.Value.Last(item => item.Role == AuthorRole.Assistant);

            var goldenDataItem = new GoldenDataItem
            {
                Query = userQuestion.Content ?? string.Empty,
                ReferenceAnswer = assistantAnswer.Content ?? string.Empty
            };

            //var assistantAndToolMessages = historyGroup.Value.Except(new[] { userQuestion, assistantAnswer }).ToArray();
            //foreach (var message in assistantAndToolMessages)
            //{
            //    if (message.Role == AuthorRole.Assistant)
            //    {
            //        if (message is OpenAIChatMessageContent openAIChatMessageContent)
            //        {
            //            if (openAIChatMessageContent.ToolCalls.Any())
            //            {
            //                var toolCallContents = openAIChatMessageContent.ToolCalls.Select(toolCall =>
            //                {
            //                    var functionName = toolCall.FunctionName;
            //                    var arguments = toolCall.FunctionArguments.ToString();

            //                    return new ToolExecution
            //                    {
            //                        Role = AuthorRole.Assistant.ToString(),
            //                        ToolCall = new ToolCall
            //                        {
            //                            ToolName = functionName.Replace("DateTools-", string.Empty).Replace("RetrievalTool-", string.Empty),
            //                            Arguments = arguments
            //                        }
            //                    };
            //                }).ToArray();

            //                goldenDataItem.ToolExecutions.AddRange(toolCallContents);
            //            }
            //        }
            //    }
            //    else if (message.Role == AuthorRole.Tool)
            //    {
            //        var toolName = GetToolName(message);

            //        goldenDataItem.ToolExecutions.Add(new ToolExecution
            //        {
            //            Role = AuthorRole.Tool.ToString(),
            //            ToolName = toolName,
            //            Content = message.Content,
            //        });
            //    }
            //}

            goldenDataset.Items.Add(goldenDataItem);
        }

        return goldenDataset;
    }
}
