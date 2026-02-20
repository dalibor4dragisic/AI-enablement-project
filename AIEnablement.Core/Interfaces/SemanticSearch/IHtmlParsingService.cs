using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch;

public interface IHtmlParsingService
{
    Task<ParsedDocument> ParseHtmlDocument(string filePath);
}
