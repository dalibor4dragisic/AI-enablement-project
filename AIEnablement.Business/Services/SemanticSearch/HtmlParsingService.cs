using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AIEnablement.Business.Services.SemanticSearch;

    public class HtmlParsingService : IHtmlParsingService
    {
        private readonly ILogger<HtmlParsingService> _logger;

        public HtmlParsingService(ILogger<HtmlParsingService> logger)
        {
            _logger = logger;
        }

        public async Task<ParsedDocument> ParseHtmlDocument(string filePath)
        {
            var html = File.ReadAllText(filePath);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var metadata = ExtractMetadata(filePath, doc);
            var sections = await ExtractSections(doc);

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

            // Extract document type from metadata div or filename
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

    //private List<ParsedSection> ExtractSections(HtmlDocument doc)
    //{
    //    var sections = new List<ParsedSection>();
    //    var body = doc.DocumentNode.SelectSingleNode("//body");

    //    if (body == null)
    //        return sections;

    //    var sectionDivs = body.SelectNodes("//div[@class='section']");

    //    if (sectionDivs == null)
    //    {
    //        // Fallback: parse by headings
    //        return ParseByHeadings(doc);
    //    }

    //    foreach (var sectionDiv in sectionDivs)
    //    {
    //        var section = ParseSection(sectionDiv);
    //        if (section != null)
    //        {
    //            sections.Add(section);
    //        }
    //    }

    //    return sections;
    //}

    private async Task<List<ParsedSection>> ExtractSections(HtmlDocument doc)
    {
        var sections = new List<ParsedSection>();
        var body = doc.DocumentNode.SelectSingleNode("//body");

        if (body == null)
            return sections;

        var sectionDivs = body.SelectNodes("//div[@class='section']");

        //if (sectionDivs == null)
        //{
        //    // Fallback: parse by headings
        //    return ParseByHeadings(doc);
        //}

        // Select all text nodes and concatenate their InnerText
        //var allText = body.SelectNodes("//text()")
        //    .Select(node => node.InnerText.Trim())
        //    .Where(text => !string.IsNullOrEmpty(text))
        //    .ToList();



        //foreach (var sectionDiv in sectionDivs)
        //{
        //    var section = ParseSection(sectionDiv, sections);
        //    if (section != null)
        //    {
        //        sections.Add(section);
        //    }
        //}

        foreach (var sectionDiv in sectionDivs)
        {
            await ParseSection(sectionDiv, sections);
        }

        return sections;
    }


    //private ParsedSection? ParseSection(HtmlNode sectionDiv)
    //    {
    //        var sectionId = sectionDiv.GetAttributeValue("id", "");

    //        // Find heading
    //        var heading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");
    //        var headingText = heading?.InnerText.Trim() ?? "Untitled Section";
    //        var level = heading != null ? int.Parse(heading.Name.Substring(1)) : 2;

    //        // Extract content (all text except nested section divs)
    //        var contentNodes = sectionDiv.SelectNodes(".//text()[not(ancestor::div[@class='section'])]");
    //        var content = contentNodes != null
    //            ? string.Join(" ", contentNodes.Select(n => CleanText(n.InnerText)))
    //            : string.Empty;

    //        if (string.IsNullOrWhiteSpace(content))
    //            return null;

    //        return new ParsedSection
    //        {
    //            SectionId = sectionId,
    //            Heading = headingText,
    //            Level = level,
    //            Content = content
    //        };
    //    }

    private async Task ParseSection1(HtmlNode sectionDiv, List<ParsedSection> parsedSections)
    {
        var sectionNodes = new string[] { "h1", "h2", "h3", "h4", "h5", "h6" };
        var textNodes = new string[] { "p", "ul", "ol" };

        // Find heading
        //var heading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");
        //var headingText = heading?.InnerText.Trim() ?? "Untitled Section";
        //var sectionId = headingText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        //var level = heading != null ? int.Parse(heading.Name.Substring(1)) : 2;

        // Extract content (all text except nested section divs)
        //var contentNodes = sectionDiv.SelectNodes(".//text()[not(ancestor::div[@class='section'])]");
        var contentNodes = sectionDiv.SelectNodes(".//div").Descendants().Where(n => textNodes.Contains(n.Name) || sectionNodes.Contains(n.Name)).ToArray();

        var firstTextNode = contentNodes.FirstOrDefault(); // Uzmi sve do prvog h
            //< div class="section" id="2.2.2">
            //<h3>2.2.2 Data Retention Policy</h3>
            //<div>
            //<p>ClariTrack Analytics retains personal data only for as long as necessary to achieve the purposes for which it was collected.Our data retention policy includes the following key components:</p>
            //<ul>
            //<li><strong>Retention Periods:</strong> Personal data is retained for the duration of the user’s account or as required by law.For example, we may retain transaction data for a minimum of seven years to comply with financial regulations.</li>
            //<li><strong>Periodic Review:</strong> We conduct periodic reviews of stored data to determine whether it is still necessary for the purposes for which it was collected. Data that is no longer needed is securely deleted or anonymized.</li>
            //<li><strong>Legal Obligations:</strong> In certain cases, we may be required to retain personal data for longer periods to comply with legal obligations, such as tax or audit requirements.</li>
            //</ul>
            //<h3>2.2.3 User Rights Regarding Data Retention</h3>

        if (firstTextNode is not null)
        {
            var heading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

            var text = firstTextNode.InnerText;

            var section = CreateSection(heading);
            section.Content = text;

            parsedSections.Add(section);
        }

        ParsedSection? subSection = null;
        StringWriter sb = null;

        var contentNodesWithoutFirst = contentNodes.Where(node => node != firstTextNode).ToList();
        foreach (var node in contentNodesWithoutFirst)
        {

            if (node.Name.StartsWith("h"))
            {
                subSection = CreateSection(node);
                sb = new StringWriter();
            }
            else if(textNodes.Contains(node.Name))
            {
                if (contentNodesWithoutFirst.Any(e => e.Name.StartsWith("h")))
                {
                    await sb.WriteLineAsync(node.InnerText);
                }
                else
                {
                    var lastSection = parsedSections.Last();
                    if (lastSection is not null)
                    {
                        sb = new StringWriter();

                        await sb.WriteLineAsync(lastSection.Content);
                        await sb.WriteLineAsync(node.InnerText);

                        lastSection.Content = sb.ToString();
                    }
                }
            }

            var nextNodeIndex = contentNodesWithoutFirst.IndexOf(node) + 1;

            if (nextNodeIndex < contentNodesWithoutFirst.Count)
            {
                var nextNode = contentNodesWithoutFirst[nextNodeIndex];

                if (nextNode.Name.StartsWith("h"))
                {
                    subSection.Content = sb.ToString();

                    parsedSections.Add(subSection);
                }
            }
            else if (subSection is not null && nextNodeIndex == contentNodesWithoutFirst.Count)
            {
                subSection.Content = sb.ToString();

                parsedSections.Add(subSection);
            }
        }


        //var content = contentNodes != null
        //    ? string.Join(" ", contentNodes.Select(n => CleanText(n.InnerText)))
        //    : string.Empty;

        //if (string.IsNullOrWhiteSpace(content))
        //    return null;

        //return new ParsedSection
        //{
        //    SectionId = sectionId,
        //    Heading = headingText,
        //    Level = level,
        //    Content = content
        //};
    }

    private async Task ParseSection(HtmlNode sectionDiv, List<ParsedSection> parsedSections)
    {
        var sectionNodes = new string[] { "h1", "h2", "h3", "h4", "h5", "h6" };
        var textNodes = new string[] { "p", "ul", "ol" };

        // Find heading
        //var heading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");
        //var headingText = heading?.InnerText.Trim() ?? "Untitled Section";
        //var sectionId = headingText.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        //var level = heading != null ? int.Parse(heading.Name.Substring(1)) : 2;

        // Extract content (all text except nested section divs)
        //var contentNodes = sectionDiv.SelectNodes(".//text()[not(ancestor::div[@class='section'])]");

        try
        {
            //var firstHeading1 = sectionDiv.ChildNodes.FirstOrDefault(nod => nod.Name.StartsWith("h"));
            var firstHeading = sectionDiv.SelectSingleNode(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

            ParsedSection ? subSection = CreateSection(firstHeading);

            //var text = Uglify.HtmlToText(sectionDiv.InnerHtml);

            subSection.Content = sectionDiv.InnerText;

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

    private List<ParsedSection> ParseByHeadings(HtmlDocument doc)
        {
            var sections = new List<ParsedSection>();
            var body = doc.DocumentNode.SelectSingleNode("//body");

            if (body == null)
                return sections;

            var headings = body.SelectNodes(".//h1|.//h2|.//h3|.//h4|.//h5|.//h6");

            if (headings == null)
                return sections;

            for (int i = 0; i < headings.Count; i++)
            {
                var heading = headings[i];
                var level = int.Parse(heading.Name.Substring(1));
                var headingText = heading.InnerText.Trim();

                // Collect content until next heading
                var content = CollectContentUntilNextHeading(heading, i < headings.Count - 1 ? headings[i + 1] : null);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    sections.Add(new ParsedSection
                    {
                        SectionId = $"section_{i + 1}",
                        Heading = headingText,
                        Level = level,
                        Content = content
                    });
                }
            }

            return sections;
        }

        private string CollectContentUntilNextHeading(HtmlNode currentHeading, HtmlNode? nextHeading)
        {
            var contentParts = new List<string>();
            var currentNode = currentHeading.NextSibling;

            while (currentNode != null && currentNode != nextHeading)
            {
                if (currentNode.NodeType == HtmlNodeType.Text)
                {
                    var text = CleanText(currentNode.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        contentParts.Add(text);
                    }
                }
                else if (currentNode.NodeType == HtmlNodeType.Element)
                {
                    if (currentNode.Name.StartsWith("h") && currentNode.Name.Length == 2)
                    {
                        break; // Stop at next heading
                    }

                    var text = CleanText(currentNode.InnerText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        contentParts.Add(text);
                    }
                }

                currentNode = currentNode.NextSibling;
            }

            return string.Join(" ", contentParts);
        }

        private string CleanText(string text)
        {
            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }
    }
