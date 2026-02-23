using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Business.Orchestrators.SemanticSearch
{

    public class SearchOrchestrator
    {
        private readonly ISearchService _searchService;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly RagConfiguration _config;
        private readonly ILogger<SearchOrchestrator> _logger;

        public SearchOrchestrator(
            ISearchService searchService,
            IVectorStoreService vectorStoreService,
            RagConfiguration config,
            ILogger<SearchOrchestrator> logger)
        {
            _searchService = searchService;
            _vectorStoreService = vectorStoreService;
            _config = config;
            _logger = logger;
        }

        public async Task RunInteractiveSearchAsync()
        {
            Console.Clear();
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine("           RAG SEMANTIC SEARCH - Phase 3                  ");
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine();

            //Load vector store
            Console.WriteLine("Loading vector store...");
            await _vectorStoreService.LoadFromDiskAsync();

            // Load chunk metadata for neighbor expansion
            LoadChunkMetadataAsync();
            Console.WriteLine("Vector store loaded");
            Console.WriteLine();

            Console.WriteLine("Configuration:");
            Console.WriteLine($"  • Top-K results: {_config.Search.TopK}");
            Console.WriteLine($"  • Min relevance score: {_config.Search.MinRelevanceScore}");
            Console.WriteLine($"  • Include neighbors: {_config.Search.IncludeNeighbors}");
            if (_config.Search.IncludeNeighbors)
            {
                Console.WriteLine($"    - Before: {_config.Search.NeighborsBefore}, After: {_config.Search.NeighborsAfter}");
            }
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  /config  - Show current configuration");
            Console.WriteLine("  /exit    - Exit search");
            Console.WriteLine();
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Query: ");
                Console.ResetColor();

                var query = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(query))
                    continue;

                if (query.ToLower() == "/exit")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                if (query.ToLower() == "/config")
                {
                    DisplayConfiguration();
                    continue;
                }

                try
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("Searching...");
                    Console.ResetColor();

                    var response = await _searchService.SearchAsync(query);

                    DisplaySearchResults(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during search");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }
        }

        private void DisplaySearchResults(SearchResponse response)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("-----------------------------------------------------------");
            Console.WriteLine($"  SEARCH RESULTS ({response.TotalResults} found in {response.SearchDuration.TotalMilliseconds:F0}ms)");
            Console.WriteLine("-----------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();

            if (response.Results.Count == 0)
            {
                Console.WriteLine("No results found matching your query.");
                return;
            }

            for (int i = 0; i < response.Results.Count; i++)
            {
                var result = response.Results[i];

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Result {i + 1}]");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Relevance Score: {result.RelevanceScore:F4}");
                Console.ResetColor();

                Console.WriteLine($"  Tool: {result.Chunk.ToolName}");
                Console.WriteLine($"  File path: {result.Chunk.FilePath}");
                Console.WriteLine($"  Document: {result.Chunk.DocumentType}");
                Console.WriteLine($"  Section: {result.Chunk.SectionHeading}");
                Console.WriteLine($"  Chunk Index: {result.Chunk.ChunkIndex}");
                Console.WriteLine($"  Tokens: {result.Chunk.TokenCount}");
                Console.WriteLine();

                // Display neighbors if present
                if (result.NeighborChunks.Any())
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  ┌─ Context (Neighbors) ─────────────────────────────────");

                    foreach (var neighbor in result.NeighborChunks.OrderBy(n => n.ChunkIndex))
                    {
                        if (neighbor.ChunkIndex < result.Chunk.ChunkIndex)
                        {
                            Console.WriteLine($"  │ [Before - Chunk {neighbor.ChunkIndex}]");
                            Console.WriteLine(neighbor.Content);
                            Console.WriteLine("  │");
                        }
                    }
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  │ [MATCHED - Chunk {result.Chunk.ChunkIndex}]");
                    Console.WriteLine(result.Chunk.Content);
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    foreach (var neighbor in result.NeighborChunks.OrderBy(n => n.ChunkIndex))
                    {
                        if (neighbor.ChunkIndex > result.Chunk.ChunkIndex)
                        {
                            Console.WriteLine("  │");
                            Console.WriteLine($"  │ [After - Chunk {neighbor.ChunkIndex}]");
                            Console.WriteLine(neighbor.Content);
                        }
                    }

                    Console.WriteLine("  └───────────────────────────────────────────────────────");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("  Content:");
                    Console.WriteLine(result.Chunk.Content);
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.WriteLine("───────────────────────────────────────────────────────────");
                Console.WriteLine();
            }
        }

        private void DisplayConfiguration()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Current Configuration:");
            Console.ResetColor();
            Console.WriteLine($"  Embedding Model: {_config.Embedding.ModelId}");
            Console.WriteLine($"  Top-K Results: {_config.Search.TopK}");
            Console.WriteLine($"  Min Relevance Score: {_config.Search.MinRelevanceScore}");
            Console.WriteLine($"  Include Neighbors: {_config.Search.IncludeNeighbors}");
            if (_config.Search.IncludeNeighbors)
            {
                Console.WriteLine($"  Neighbors Before: {_config.Search.NeighborsBefore}");
                Console.WriteLine($"  Neighbors After: {_config.Search.NeighborsAfter}");
            }
            Console.WriteLine($"  Chunk Size: {_config.Chunking.ChunkSize} tokens");
            Console.WriteLine($"  Chunk Overlap: {_config.Chunking.ChunkOverlap} tokens");
            Console.WriteLine();
        }

        private void LoadChunkMetadataAsync()
        {
            var chunks = _vectorStoreService.GetChunks();

            _searchService.IndexChunksForNeighborLookup(chunks);
        }
    }
}
