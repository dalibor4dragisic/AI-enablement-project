using System.Text;
using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Models;
using AIEnablement.Core.Models.DocumentGeneration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIEnablement.Business.Orchestrators;

public class DocumentGenerationOrchestrator
{
    private readonly IToolIdeationService _toolIdeationService;
    private readonly ITocGenerationService _tocGenerationService;
    private readonly ISectionGenerationService _sectionGenerationService;
    private readonly IDocumentAssemblyService _documentAssemblyService;
    private readonly AppConfiguration _config;
    private readonly ILogger<DocumentGenerationOrchestrator> _logger;

    public DocumentGenerationOrchestrator(
        IToolIdeationService toolIdeationService,
        ITocGenerationService tocGenerationService,
        ISectionGenerationService sectionGenerationService,
        IDocumentAssemblyService documentAssemblyService,
        AppConfiguration config,
        ILogger<DocumentGenerationOrchestrator> logger)
    {
        _toolIdeationService = toolIdeationService;
        _tocGenerationService = tocGenerationService;
        _sectionGenerationService = sectionGenerationService;
        _documentAssemblyService = documentAssemblyService;
        _config = config;
        _logger = logger;
    }

    public async Task GenerateAllDocumentsAsync()
    {
        const string toolIdeationFileName = "tools_ideation.json";

        var tools = await _toolIdeationService.GenerateToolDescriptionsAsync();

        var outputDir = _config.Generation.OutputDirectory;
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        Directory.CreateDirectory(outputDir);

        // Save tools ideation as JSON
        var tocPath = Path.Combine(outputDir, toolIdeationFileName);
        var tocJson = JsonSerializer.Serialize(tools, new JsonSerializerOptions { WriteIndented = true });
        
        await File.WriteAllTextAsync(tocPath, tocJson);
        _logger.LogInformation("Tools ideation saved: {FileName}",
            toolIdeationFileName);

        foreach (var tool in tools)
        {
            var toolDir = Path.Combine(outputDir, SanitizeFileName(tool.Name));
            Directory.CreateDirectory(toolDir);

            try
            {
                _logger.LogInformation("Generating tool {Tool}...", tool.Name);

                await GenerateTocAsync(tool, toolDir);

                _logger.LogInformation("Completed all sections for {Tool}", tool.Name);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate tool {Tool}", tool.Name);
            }
        }

        _logger.LogInformation("-----");
        _logger.LogInformation("Generation complete for {ToolCount} tools", tools.Count);
        _logger.LogInformation("-----");
    }

    private async Task GenerateTocAsync(ToolDescription tool, string outputDir)
    {
        // Generate ToC structure
        var toc = await _tocGenerationService.GenerateTocAsync(tool);
        
        //// Save ToC as JSON file
        //var tocFileName = $"toc_{SanitizeFileName(tool.Name)}.json";
        //var tocPath = Path.Combine(outputDir, tocFileName);
        //var tocJson = JsonSerializer.Serialize(toc, new JsonSerializerOptions { WriteIndented = true });
        //await File.WriteAllTextAsync(tocPath, tocJson);

        // Generate HTML sections
        _logger.LogInformation("Generating sections...");

        var context = new DocumentGenerationContext
        {
            Tool = tool,
            Toc = toc,
            GeneratedSections = new List<GeneratedSection>()
        };

        await GenerateSectionsRecursivelyAsync(context, toc.Sections, outputDir);

        // Write tool content to file
        var documentContext = JsonSerializer.Serialize(context, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(outputDir, $"tool_{SanitizeFileName(tool.Name)}.json"), documentContext);

        // Write injected issues to file
        var issues = CreateStringWithIssues(context.Toc.Sections);
        if (!string.IsNullOrEmpty(issues))
        {
            var issuesPath = Path.Combine(outputDir, "issues.txt");
            await File.WriteAllTextAsync(issuesPath, issues);
        }
    }

    private async Task GenerateSectionsRecursivelyAsync(DocumentGenerationContext documentContext, List<TocSection> tocSections, string outputDir)
    {
        foreach (var tocSection in tocSections)
        {
            var generatedSection = await _sectionGenerationService.GenerateSectionAsync(documentContext, tocSection);

            tocSection.GeneratedSection = generatedSection;

            if (tocSection.Subsections.Any())
            {
                await GenerateSectionsRecursivelyAsync(documentContext, tocSection.Subsections, outputDir);
            }

            if (tocSection.Level == 1)
            {
                await SaveTocTopLevelSection(tocSection, outputDir);

                await SaveHtmlTopLevelSection(documentContext, tocSections, tocSection, outputDir);
            }
        }
    }

    private async Task SaveTocTopLevelSection(TocSection tocSection, string outputDir)
    {
        var sectionTitle = SanitizeFileName(tocSection.Title);
        var tocFileName = $"toc_{sectionTitle}.json";
        var tocPath = Path.Combine(outputDir, tocFileName);
        var tocJson = JsonSerializer.Serialize(tocSection, new JsonSerializerOptions { WriteIndented = true });

        await File.WriteAllTextAsync(tocPath, tocJson);
        _logger.LogInformation("ToC section saved: {Section} top-level section, file: {FileName}",
            tocSection.Title, tocFileName);
    }

    private async Task SaveHtmlTopLevelSection(DocumentGenerationContext documentContext,
        List<TocSection> tocSections,
        TocSection tocSection,
        string outputDir)
    {
        var sectionIndex = tocSections.IndexOf(tocSection);
        var html = _documentAssemblyService.AssembleDocument(documentContext, tocSection, shouldAddMetadata: sectionIndex == 0);

        var sectionTitle = SanitizeFileName(tocSection.Title);
        var htmlFileName = $"{sectionTitle}.html";
        var htmlPath = Path.Combine(outputDir, htmlFileName);
        await File.WriteAllTextAsync(htmlPath, html);

        _logger.LogInformation("HTML section saved: {Section} top-level section, file {FileName}",
            tocSection.Title, htmlFileName);
    }

    private string CreateStringWithIssues(List<TocSection> sections)
    {
        var stringBuilder = new StringBuilder();

        GetAllIssues(sections, stringBuilder);

        return stringBuilder.ToString();
    }

    private void GetAllIssues(List<TocSection> sections, StringBuilder stringBuilder)
    {
        foreach (var section in sections)
        {
            if (section.GeneratedSection is not null 
                && section.GeneratedSection.InjectedIssues.Any())
            {
                stringBuilder.AppendLine($"Issues for {section.Title}:");

                foreach (var issue in section.GeneratedSection.InjectedIssues)
                {
                    stringBuilder.AppendLine(issue);
                }
            }

            if (section.Subsections.Any())
            {
                GetAllIssues(section.Subsections, stringBuilder);
            }
        }
    }

    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new InvalidOperationException("File name can't be empty or null.");
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.ToLowerInvariant().Replace(" ", "_").Replace(".", "");
    }
}
