using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models;
using AIEnablement.Utility.Extensions;
using AIEnablement.Utility.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace AIEnablement.Infrastructure.Services;

public class LlmService : ILlmService
{
    private readonly IValidationService _validationService;
    private readonly AppConfiguration _config;
    private readonly Kernel _kernel;

    public LlmService(AppConfiguration config, IValidationService validationService)
    {
        _config = config;
        _validationService = validationService;

        var builder = Kernel.CreateBuilder();

        AddAModelServices(config, builder);

        _kernel = builder.Build();
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, ModelSettings modelSettings)
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>(modelSettings.ModelId);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = modelSettings.Temperature,
            TopP = modelSettings.TopP,
            ServiceId = modelSettings.ModelId
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel
        );

        return result.Content ?? throw new InvalidOperationException("LLM returned null content");
    }

    public async Task<T> GenerateStructuredOutputAsync<T>(string systemPrompt, string userPrompt, ModelSettings modelSettings) where T : class
    {
        var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>(modelSettings.ModelId);
        
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = modelSettings.Temperature,
            TopP = modelSettings.TopP,
            ServiceId = modelSettings.ModelId
        };

        var result = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel
        );

        var content = result.Content ?? throw new InvalidOperationException("LLM returned null content");

        var cleanJson = result.Content.CleanJson();

        if (!_validationService.ValidateJson(cleanJson, out var jsonErrors))
        {
            throw new InvalidOperationException($"JSON validation failed: {string.Join(", ", jsonErrors)}");
        }

        return JsonSerializer.Deserialize<T>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize structured output");
    }

    private void AddAModelServices(AppConfiguration config, IKernelBuilder builder)
    {
        var models = config.Models.Values.Select(modelSettings => modelSettings.ModelId)
            .Distinct()
            .ToArray();

        foreach (var model in models)
        {
            builder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: _config.ApiKeys.OpenAI,
                endpoint: new Uri(_config.ApiEndpoint.Endpoint),
                serviceId: model
            );
        }
    }
}
