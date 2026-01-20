using AIEnablement.Core.Models.Chatbot;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using AIEnablement.Infrastructure.Plugins;
using AIEnablement.Core.Interfaces.Chatbot;

namespace AIEnablement.Infrastructure.Services;

public class LLMChatService : ILLMChatService
{
    private readonly Kernel _kernel;
    private readonly ChatbotConfiguration _config;
    private readonly ILogger<LLMChatService> _logger;

    public LLMChatService(
        ChatbotConfiguration config,
        DateToolsPlugin dateToolsPlugin,
        ILogger<LLMChatService> logger)
    {
        _config = config;
        _logger = logger;

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
    }

    public async Task<string> GetResponseAsync(List<ChatContent> chatHistoryContent)
    {
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
            var chatHistory = CreateChatHistory(chatHistoryContent);

            var result = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel
            );

            return result.Content ?? throw new InvalidOperationException("LLM returned null content");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response");
            throw;
        }
    }

    private ChatHistory CreateChatHistory(List<ChatContent> chatHistoryContent)
    {
        var history = new ChatHistory();

        foreach (var message in chatHistoryContent)
        {
            switch (message.Role)
            {
                case ChatRole.System:
                    history.AddSystemMessage(message.Content);
                    break;
                case ChatRole.User:
                    history.AddUserMessage(message.Content);
                    break;
                case ChatRole.Assistant:
                    history.AddAssistantMessage(message.Content);
                    break;
            }
        }

        return history;
    }
}
