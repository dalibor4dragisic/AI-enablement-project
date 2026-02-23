using System.Text.RegularExpressions;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Infrastructure.Services.SemanticSearch;

    public class HtmlParsingService : IHtmlParsingService
    {
        private readonly ILogger<HtmlParsingService> _logger;

        public HtmlParsingService(ILogger<HtmlParsingService> logger)
        {
            _logger = logger;
        }

        public ParsedDocument ParseHtmlDocument(string filePath)
        {
            var html = File.ReadAllText(filePath);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var metadata = ExtractMetadata(filePath, doc);
            var sections = ExtractSections(doc);

            var sortedSections = sections.OrderBy(section => section.SectionId).ToList();

            metadata.SectionId = sortedSections.First().SectionId;

            return new ParsedDocument
            {
                Metadata = metadata,
                Sections = sortedSections
            };
        }

        private DocumentMetadata ExtractMetadata(string filePath, HtmlDocument doc)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var toolName = Path.GetFileName(Path.GetDirectoryName(filePath)) ?? "Unknown";

            var metadataDiv = doc.DocumentNode.SelectSingleNode("//div[@class='metadata']");
            var documentType = fileName.Replace("_", " ");

            if (metadataDiv != null)
            {
                var docTypeMatch = Regex.Match(metadataDiv.InnerText, @"Document:\s*(.+?)(?:\n|<br|$)");
                if (docTypeMatch.Success)
                {
                    documentType = docTypeMatch.Groups[1].Value.Trim();
                }
            }

            return new DocumentMetadata
            {
                ToolName = toolName,
                DocumentType = documentType,
                FilePath = filePath
            };
        }

    private List<ParsedSection> ExtractSections(HtmlDocument doc)
    {
        var sections = new List<ParsedSection>();
        var body = doc.DocumentNode.SelectSingleNode("//body");

        if (body == null)
        {
            return sections;
        }

        var sectionDivs = body.SelectNodes("//div[@class='section']");

        foreach (var sectionDiv in sectionDivs)
        {
            ParseSection(sectionDiv, sections);
        }

        return sections;
    }

    private void ParseSection(HtmlNode sectionDiv, List<ParsedSection> parsedSections)
    {
        try
        {
            var firstHeading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

            var subSection = CreateSection(firstHeading);

            subSection.Content = CleanText(sectionDiv.InnerText);

            parsedSections.Add(subSection);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private ParsedSection CreateSection(HtmlNode heading)
    {
        var headingText = heading?.InnerText.Trim() ?? "Untitled Section";
        var sectionId = headingText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        var level = heading != null ? int.Parse(heading.Name.Substring(1)) : 2;

        return new ParsedSection
        {
            SectionId = sectionId,
            Heading = headingText,
            Level = level
        };
    }

    private string CleanText(string text)
    {
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Trim();
        return text;
    }
}
