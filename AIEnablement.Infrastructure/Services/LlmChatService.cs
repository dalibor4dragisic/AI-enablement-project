using AIEnablement.Core.Interfaces.Chatbot;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models.Chatbot;
using AIEnablement.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;

namespace AIEnablement.Infrastructure.Services;

public class LLMChatService : ILLMChatService
{
    private readonly Kernel _kernel;
    private readonly ChatbotConfiguration _config;
    private readonly ILogger<LLMChatService> _logger;
    private readonly IPromptService _promptService;
    private readonly ChatHistory _chatHistory = new();
    private readonly Dictionary<ChatMessageContent, DateTime> _chatMessagesDates = new();
    private readonly DateTime _conversationStarted;

    public LLMChatService(
        ChatbotConfiguration config,
        DateToolsPlugin dateToolsPlugin,
        ILogger<LLMChatService> logger,
        IPromptService promptService)
    {
        _config = config;
        _logger = logger;
        _promptService = promptService;

        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            modelId: config.Model.ModelId,
            endpoint: new Uri(_config.ApiEndpoint.Endpoint),
            apiKey: config.ApiKeys.OpenAI
        );

        // Add plugins if tools are enabled
        if (config.Assistant.EnableTools && config.Tools["DateTools"].Enabled)
        {
            builder.Plugins.AddFromObject(dateToolsPlugin, "DateTools");
        }

        _kernel = builder.Build();

        LoadSystemPrompt();

        _conversationStarted = DateTime.Now;
    }

    public async Task<string> GetResponseAsync(string message)
    {
        await TrimHistoryWithCompactionAsync();

        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

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

    public async Task<ConversationSummary> GetSummary()
    {
        const string getCurrentDate = "get_current_date";
        const string addDaysToDate = "add_days_to_date";
        var assistantMessages = _chatHistory.Where(chatContent => chatContent.Role == AuthorRole.Assistant).ToList();
        var userMessages = _chatHistory.Count(chatContent => chatContent.Role == AuthorRole.User);
        var assistantMessagesCount = assistantMessages.Count();
        var systemMessages = _chatHistory.Count(chatContent => chatContent.Role == AuthorRole.System);

        var getCurrentDateToolCalls = 0;
        var addDaysToDateToolCalls = 0;

        foreach (var message in _chatHistory.Where(historyItem => historyItem.Role == AuthorRole.Tool))
        {
            foreach (var item in message.Items)
            {
                if (item is FunctionResultContent functionResultContent)
                {
                    // The function name is available within the function call metadata
                    var functionName = functionResultContent.FunctionName;

                    if (functionName == getCurrentDate)
                    {
                        getCurrentDateToolCalls++;
                    }
                    else if (functionName == addDaysToDate)
                    {
                        addDaysToDateToolCalls++;
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
            ConversationStarted = _conversationStarted,
            Duration = DateTime.Now - _conversationStarted,
            SummaryText = summaryMessage
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

        var result = await GetConversationSummaryPreserveAsync(conversationHistory);

        return result.Content ?? string.Empty;
    }

    private async Task<ChatMessageContent> GetConversationSummaryPreserveAsync(ChatMessageContent[] conversationHistory)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.Model.Temperature,
            MaxTokens = _config.Model.MaxTokens,
            TopP = _config.Model.TopP
        };

        var chatContents = GetAllMessages(conversationHistory);
        var conversationText = GetConversationHistoryOld(chatContents);

        var promptData = _promptService.LoadPromptFromFile(_config.Assistant.SummaryPreservePromptPath);
        var systemPromptTemplate = promptData["system_prompt"];
        var userPromptTemplate = promptData["user_prompt"];

        var userPrompt = userPromptTemplate.Replace("{conversation_text}", conversationText);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPromptTemplate);
        chatHistory.AddUserMessage(userPrompt);
        
        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel
        );

        return result ?? throw new InvalidOperationException("LLM returned null content");
    }

    private async Task TrimHistoryWithCompactionAsync()
    {
        var groupedMessages = GetGroupedMessages(_chatHistory);

        if (groupedMessages.Count() < _config.Assistant.MaxHistoryMessages)
        {
            return;
        }

        var messagesToCompress = groupedMessages.Count() - (_config.Assistant.MaxHistoryMessages / 2);

        var oldMessages = groupedMessages.Take(messagesToCompress).SelectMany(pair => pair.Value).ToList();
        var recentMessages = groupedMessages.Skip(messagesToCompress).SelectMany(pair => pair.Value).ToList();

        var result = await GetConversationSummaryPreserveAsync(oldMessages.ToArray());

        var systemMessage = _chatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);

        _chatHistory.Clear();

        if (systemMessage != null)
        {
            _chatHistory.Add(systemMessage);
        }

        _chatHistory.Add(result);

        _chatHistory.AddRange(recentMessages);

        RefreshMessageTimeStamps();
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
            else if (chatMessage.Role == AuthorRole.Assistant
                     && chatMessage.Content != null
                     && chatMessages.ToList().IndexOf(chatMessage) == 1)
            {
                messagesGroup.TryAdd(groupNumber, [chatMessage]);
                groupNumber++;
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

                            return $"[Called function: {functionName.Replace("DateTools-", "")}, arguments: {arguments}, id: {toolCall.Id}]";
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

    private string GetConversationHistoryOld(IReadOnlyCollection<ChatContent> chatContents)
    {
        var sb = new StringBuilder();

        foreach (var chatContent in chatContents)
        {
            sb.AppendLine($"{chatContent.Role}: {chatContent.Content}");
        }

        return sb.ToString();
    }

    private void LoadSystemPrompt()
    {
        var data = _promptService.LoadPromptFromFile(_config.Assistant.PersonalityPromptPath);

        var systemPrompt = data["system_prompt"];
        _chatHistory.AddSystemMessage(systemPrompt);

        var systemMessage = _chatHistory.FirstOrDefault(chat => chat.Role == AuthorRole.System);
        if (systemMessage != null)
        {
            _chatMessagesDates.TryAdd(systemMessage, DateTime.Now);
        }
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
}
