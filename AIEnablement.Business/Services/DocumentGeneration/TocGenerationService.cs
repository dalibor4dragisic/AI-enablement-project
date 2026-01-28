using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models;
using AIEnablement.Core.Models.DocumentGeneration;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Business.Services.DocumentGeneration;

public class TocGenerationService : ITocGenerationService
{
    private readonly ILLMDocumentService _llmService;
    private readonly IPromptService _promptService;
    private readonly AppConfiguration _config;
    private readonly ILogger<TocGenerationService> _logger;

    public TocGenerationService(
        ILLMDocumentService llmService,
        IPromptService promptService,
        AppConfiguration config,
        ILogger<TocGenerationService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _config = config;
        _logger = logger;
    }

    public async Task<TableOfContents> GenerateTocAsync(ToolDescription tool)
    {
        var promptData = _promptService.LoadPromptFromFile(_config.Prompts.TocGenerationPromptPath);

        var systemPromptTemplate = promptData["system_prompt"];
        var systemPrompt = systemPromptTemplate
            .Replace("{available_document_types}", string.Join(", ", tool.DocumentTypes))
            .Replace("{tool_name}", tool.Name);


        var userPromptTemplate = promptData["user_prompt"];
        var userPrompt = userPromptTemplate
            .Replace("{tool_name}", tool.Name)
            .Replace("{tool_purpose}", tool.Purpose)
            .Replace("{tool_category}", tool.Category)
            .Replace("{user_base}", tool.TypicalUserBase)
            .Replace("{number_of_sections}", _config.Generation.DocumentsPerTool.ToString());

        var modelSettings = _config.Models["TocGenerationModel"];

        var toc = await _llmService.GenerateStructuredOutputAsync<TableOfContents>(
            systemPrompt,
            userPrompt,
            modelSettings
        );

        _logger.LogInformation("Generated ToC with {Count} top-level sections", toc.Sections.Count);

        return toc;
    }
}
