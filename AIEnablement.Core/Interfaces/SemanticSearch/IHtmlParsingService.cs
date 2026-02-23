using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch;

public interface IHtmlParsingService
{
    ParsedDocument ParseHtmlDocument(string filePath);
}
