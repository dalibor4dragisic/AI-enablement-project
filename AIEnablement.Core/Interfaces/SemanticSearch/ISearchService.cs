using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch;

public interface ISearchService
{
    Task<SearchResponse> SearchAsync(string query, int? topK = null, bool? includeNeighbors = null, double? minRelevanceScore = null);
    void IndexChunksForNeighborLookup(List<TextChunk> chunks);
}
