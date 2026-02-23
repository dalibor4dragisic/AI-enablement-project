using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch;

public interface IChunkingService
{
    List<TextChunk> ChunkDocument(ParsedDocument document);
}