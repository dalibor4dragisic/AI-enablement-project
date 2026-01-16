using System.Text;
using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Business.Services.DocumentGeneration;

public class DocumentAssemblyService : IDocumentAssemblyService
{

    public DocumentAssemblyService()
    {

    }

    public string AssembleDocument(DocumentGenerationContext context, TocSection tocSection, bool shouldAddMetadata)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"    <title>{context.Toc.Title} - {context.Tool.Name}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: Arial, sans-serif; line-height: 1.6; max-width: 900px; margin: 0 auto; padding: 20px; }");
        sb.AppendLine("        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; }");
        sb.AppendLine("        h2 { color: #34495e; margin-top: 30px; border-left: 4px solid #3498db; padding-left: 10px; }");
        sb.AppendLine("        h3 { color: #555; margin-top: 20px; }");
        sb.AppendLine("        h4 { color: #666; margin-top: 15px; }");
        sb.AppendLine("        p { margin: 10px 0; text-align: justify; }");
        sb.AppendLine("        .metadata { background: #ecf0f1; padding: 15px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine("        .section { margin-bottom: 25px; }");
        sb.AppendLine("        ul, ol { margin: 10px 0; padding-left: 30px; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        if (shouldAddMetadata)
        {
            sb.AppendLine("    <div class=\"metadata\">");
            sb.AppendLine($"        <strong>Tool:</strong> {context.Tool.Name}<br>");
            sb.AppendLine($"        <strong>Category:</strong> {context.Tool.Category}<br>");
            sb.AppendLine($"        <strong>Generated:</strong> {DateTime.UtcNow:yyyy-MM-dd}");
            sb.AppendLine("    </div>");

            sb.AppendLine($"    <h1>{context.Toc.Title}</h1>");
        }

        var allSections = GetAllHtmlSections(tocSection);

        foreach (var section in allSections)
        {
            if (section.GeneratedSection is not null)
            {
                sb.AppendLine($"    <div class=\"section\" id=\"{section.Id}\">");
                sb.AppendLine(section.GeneratedSection.HtmlContent);
                sb.AppendLine("    </div>");
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        var html = sb.ToString();
        
        return html;
    }

    private static List<TocSection> GetAllHtmlSections(TocSection tocSection)
    {
        var sections = new List<TocSection> { tocSection };

        foreach (var subSection in tocSection.Subsections)
        {
            var subSections = GetAllHtmlSections(subSection);

            sections.AddRange(subSections);
        }

        return sections;
    }
}
