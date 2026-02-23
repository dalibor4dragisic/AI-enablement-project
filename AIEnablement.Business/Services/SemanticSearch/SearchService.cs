using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIEnablement.Business.Services.SemanticSearch
{
    public class SearchService : ISearchService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly RagConfiguration _config;
        private readonly ILogger<SearchService> _logger;
        private readonly Dictionary<string, List<TextChunk>> _chunksByDocument = new();

        public SearchService(
            IEmbeddingService embeddingService,
            IVectorStoreService vectorStoreService,
            RagConfiguration config,
            ILogger<SearchService> logger)
        {
            _embeddingService = embeddingService;
            _vectorStoreService = vectorStoreService;
            _config = config;
            _logger = logger;
        }

        public void IndexChunksForNeighborLookup(List<TextChunk> chunks)
        {
            _chunksByDocument.Clear();

            foreach (var chunk in chunks)
            {
                var key = $"{chunk.ToolName}|{chunk.DocumentType}";
                if (!_chunksByDocument.ContainsKey(key))
                {
                    _chunksByDocument[key] = new List<TextChunk>();
                }
                _chunksByDocument[key].Add(chunk);
            }

            foreach (var key in _chunksByDocument.Keys)
            {
                _chunksByDocument[key] = _chunksByDocument[key].OrderBy(c => c.ChunkIndex).ToList();
            }

            _logger.LogInformation("Indexed {Count} documents for neighbor lookup", _chunksByDocument.Count);
        }

        public async Task<SearchResponse> SearchAsync(string query)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Generating embedding for query: {Query}", query);
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            _logger.LogInformation("Searching for top {TopK} results", _config.Search.TopK);
            var searchResults = await _vectorStoreService.SearchAsync(queryEmbedding, _config.Search.TopK);

            //Build results with neighbors if configured
            var results = new List<SearchResult>();

            foreach (var (chunk, score) in searchResults)
            {
                if (score < _config.Search.MinRelevanceScore)
                    continue;

                var result = new SearchResult
                {
                    Chunk = chunk,
                    RelevanceScore = score,
                    NeighborChunks = new List<TextChunk>()
                };

                //Add neighbors if configured
                if (_config.Search.IncludeNeighbors)
                {
                    result.NeighborChunks = GetNeighborChunks(chunk);
                }

                results.Add(result);
            }

            stopwatch.Stop();

            return new SearchResponse
            {
                Query = query,
                Results = results,
                TotalResults = results.Count,
                SearchDuration = stopwatch.Elapsed
            };
        }

        private List<TextChunk> GetNeighborChunks(TextChunk chunk)
        {
            var neighbors = new List<TextChunk>();
            var key = $"{chunk.ToolName}|{chunk.DocumentType}";

            if (!_chunksByDocument.TryGetValue(key, out List<TextChunk>? documentChunks))
            {
                return neighbors;
            }

            var currentIndex = documentChunks.FindIndex(c => c.Id == chunk.Id);

            if (currentIndex == -1)
            {
                return neighbors;
            }

            //Get neighbors before
            for (int i = Math.Max(0, currentIndex - _config.Search.NeighborsBefore); i < currentIndex; i++)
            {
                neighbors.Add(documentChunks[i]);
            }

            //Get neighbors after
            for (int i = currentIndex + 1; i <= Math.Min(documentChunks.Count - 1, currentIndex + _config.Search.NeighborsAfter); i++)
            {
                neighbors.Add(documentChunks[i]);
            }

            return neighbors;
        }
    }
}
