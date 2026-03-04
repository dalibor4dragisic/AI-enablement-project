using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string query, int? topK = null);
    void IndexChunksForNeighborLookup(List<TextChunk> chunks);
}
