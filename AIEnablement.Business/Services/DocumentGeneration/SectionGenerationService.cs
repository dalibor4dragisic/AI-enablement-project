using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Models;
using AIEnablement.Core.Models.DocumentGeneration;
using Microsoft.Extensions.Logging;
using System.Text;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Utility.Services;

namespace AIEnablement.Business.Services.DocumentGeneration;

public class SectionGenerationService : ISectionGenerationService
{
    private readonly ILlmService _llmService;
    private readonly IPromptService _promptService;
    private readonly IValidationService _validationService;
    private readonly AppConfiguration _config;
    private readonly ILogger<SectionGenerationService> _logger;

    public SectionGenerationService(
        ILlmService llmService,
        IPromptService promptService,
        IValidationService validationService,
        AppConfiguration config,
        ILogger<SectionGenerationService> logger)
    {
        _llmService = llmService;
        _promptService = promptService;
        _validationService = validationService;
        _config = config;
        _logger = logger;
    }

    public async Task<GeneratedSection> GenerateSectionAsync(DocumentGenerationContext context, TocSection section)
    {
        _logger.LogInformation("Generating section: {SectionId} - {Title}", section.Id, section.Title);

        var promptData = _promptService.LoadPromptFromFile(_config.Prompts.SectionGenerationPromptPath);
        var systemPromptTemplate = promptData["system_prompt"].ToString() ?? string.Empty;
        var userPromptTemplate = promptData["user_prompt"].ToString() ?? string.Empty;

        var previousSectionsHtml = BuildPreviousSectionsHtmlContext(context.Toc.Sections);

        var systemPrompt = systemPromptTemplate
            .Replace("{section_level}", section.Level.ToString());

        var shouldInjectIssue = section.ShouldHaveIssue.ToString().ToLower();

        var userPrompt = userPromptTemplate
            .Replace("{section_title}", section.Title)
            .Replace("{section_level}", section.Level.ToString())
            .Replace("{inject_issue}", shouldInjectIssue)
            .Replace("{tool_name}", context.Tool.Name)
            .Replace("{tool_purpose}", context.Tool.Purpose)
            .Replace("{tool_category}", context.Tool.Category)
            .Replace("{user_base}", context.Tool.TypicalUserBase)
            .Replace("{previous_sections}", previousSectionsHtml);

        var modelSettings = _config.Models["SectionGenerationModel"];

        var response = await _llmService.GenerateStructuredOutputAsync<GeneratedSectionResponse>(
            systemPrompt,
            userPrompt,
            modelSettings
        );

        if (!_validationService.ValidateHtml(response.SectionHtml, out var htmlErrors))
        {
            _logger.LogWarning("HTML validation warnings: {Errors}",
                string.Join(", ", htmlErrors));
        }

        return new GeneratedSection
        {
            SectionId = section.Id,
            Title = section.Title,
            HtmlContent = response.SectionHtml,
            InjectedIssues = response.IssuesApplied
        };
    }

    private static string BuildPreviousSectionsHtmlContext(List<TocSection> tocTopLevelSections)
    {
        if (tocTopLevelSections.All(topLevelSection => topLevelSection.GeneratedSection is null))
        {
            return "No previous sections yet. This is the first section of the document.";
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("Previously generated sections:");

        foreach (var tocSection in tocTopLevelSections.Where(tocTopLevelSection => tocTopLevelSection.GeneratedSection is not null))
        {
            BuildPreviousSubSections(stringBuilder, tocSection);
        }

        return stringBuilder.ToString();
    }

    private static void BuildPreviousSubSections(StringBuilder stringBuilder, TocSection tocSection)
    {
        if (tocSection.GeneratedSection is null)
        {
            return;
        }

        stringBuilder.AppendLine($"\n--- {tocSection.Title} ---");
        stringBuilder.AppendLine(tocSection.GeneratedSection.HtmlContent);

        foreach (var subSection in tocSection.Subsections.Where(tocSubsection => tocSubsection.GeneratedSection is not null))
        {
            BuildPreviousSubSections(stringBuilder, subSection);
        }
    }
}
