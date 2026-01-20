using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models;
using AIEnablement.Core.Models.DocumentGeneration;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Business.Services.DocumentGeneration;

public class ToolIdeationService : IToolIdeationService
{
    private readonly ILLMDocumentService _llmService;
    private readonly IPromptService _promptService;
    private readonly AppConfiguration _config;
    private readonly ILogger<ToolIdeationService> _logger;
    private readonly Random _random = new();

    public ToolIdeationService(
        ILLMDocumentService llmService,
        IPromptService promptService,
        AppConfiguration config,
        ILogger<ToolIdeationService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _config = config;
        _logger = logger;
    }

    public async Task<List<ToolDescription>> GenerateToolDescriptionsAsync()
    {
        _logger.LogInformation("Generating {Count} tool descriptions...", _config.Generation.NumberOfTools);

        var promptData = _promptService.LoadPromptFromFile(_config.Prompts.ToolIdeationPromptPath);
        var systemPrompt = promptData["system_prompt"];
        var userPromptTemplate = promptData["user_prompt"];
        var userPrompt = userPromptTemplate
            .Replace("{number_of_tools}", _config.Generation.NumberOfTools.ToString())
            .Replace("{available_document_types}", string.Join(", ", _config.Generation.AvailableDocumentTypes));

        var modelSettings = _config.Models["ToolIdeationModel"];

        var response = await _llmService.GenerateStructuredOutputAsync<ToolIdeationResponse>(
            systemPrompt,
            userPrompt,
            modelSettings
        );

        var tools = response.Tools.Select(toolDescription =>
        {
            var docTypes = SelectRandomDocumentTypes(_config.Generation.DocumentsPerTool);
            toolDescription.DocumentTypes = docTypes;

            return toolDescription;
        }).ToList();

        return tools;
    }

    private List<string> SelectRandomDocumentTypes(int numberOfTools)
    {
        var availableDocumentTypes = _config.Generation.AvailableDocumentTypes.ToArray();

        _random.Shuffle(availableDocumentTypes);

        var numberOfToolsToTake = numberOfTools > availableDocumentTypes.Length ? availableDocumentTypes.Length : numberOfTools;

        var selectedDocumentTypes = availableDocumentTypes.Take(numberOfToolsToTake).ToList();

        return selectedDocumentTypes;
    }
}