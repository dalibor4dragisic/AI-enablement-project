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

            // Load vector store
            Console.WriteLine("📂 Loading vector store...");
            await _vectorStoreService.LoadFromDiskAsync();

            // Load chunk metadata for neighbor expansion
            await LoadChunkMetadataAsync();
            Console.WriteLine("   ✓ Vector store loaded");
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
                    Console.WriteLine("🔍 Searching...");
                    Console.ResetColor();

                    var response = await _searchService.SearchAsync(query);

                    DisplaySearchResults(response);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during search");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ Error: {ex.Message}");
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
                            //DisplayChunkContent(neighbor.Content, "  │ ", 150);
                            Console.WriteLine(neighbor.Content);
                            Console.WriteLine("  │");
                        }
                    }
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"  │ [MATCHED - Chunk {result.Chunk.ChunkIndex}]");
                    //DisplayChunkContent(result.Chunk.Content, "  │ ", 300);
                    Console.WriteLine(result.Chunk.Content);
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    foreach (var neighbor in result.NeighborChunks.OrderBy(n => n.ChunkIndex))
                    {
                        if (neighbor.ChunkIndex > result.Chunk.ChunkIndex)
                        {
                            Console.WriteLine("  │");
                            Console.WriteLine($"  │ [After - Chunk {neighbor.ChunkIndex}]");
                            //DisplayChunkContent(neighbor.Content, "  │ ", 150);
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
                    //DisplayChunkContent(result.Chunk.Content, "    ", 500);
                    Console.WriteLine(result.Chunk.Content);
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.WriteLine("───────────────────────────────────────────────────────────");
                Console.WriteLine();
            }
        }

        private void DisplayChunkContent(string content, string prefix, int maxLength)
        {
            var lines = WrapText(content, 80);
            var displayedLength = 0;

            foreach (var line in lines)
            {
                if (displayedLength + line.Length > maxLength)
                {
                    var remaining = maxLength - displayedLength;
                    if (remaining > 0)
                    {
                        Console.WriteLine($"{prefix}{line.Substring(0, remaining)}...");
                    }
                    break;
                }

                Console.WriteLine($"{prefix}{line}");
                displayedLength += line.Length;
            }
        }

        private List<string> WrapText(string text, int maxWidth)
        {
            var lines = new List<string>();
            var words = text.Split(' ');
            var currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + word).Length > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        lines.Add(currentLine.Trim());
                        currentLine = "";
                    }
                }

                currentLine += word + " ";
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine.Trim());
            }

            return lines;
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

        private async Task LoadChunkMetadataAsync()
        {
            //var metadataPath = Path.Combine(_config.Ingestion.VectorStoreDirectory, "chunk_metadata.json");

            //if (!File.Exists(metadataPath))
            //{
            //    _logger.LogWarning("Chunk metadata not found, neighbor expansion may not work correctly");
            //    return;
            //}

            //var json = await File.ReadAllTextAsync(metadataPath);
            //var metadata = JsonSerializer.Deserialize<List<ChunkMetadata>>(json, new JsonSerializerOptions
            //{
            //    PropertyNameCaseInsensitive = true
            //});

            //if (metadata == null)
            //    return;

            //// Convert to TextChunks for neighbor lookup
            //var chunks = metadata.Select(m => new TextChunk
            //{
            //    Id = m.Id,
            //    ToolName = m.ToolName,
            //    DocumentType = m.DocumentType,
            //    FilePath = m.FilePath,
            //    SectionHeading = m.SectionHeading,
            //    ChunkIndex = m.ChunkIndex,
            //    TokenCount = m.TokenCount,
            //    Content = "" // Content will be loaded from vector store when needed
            //}).ToList();

            //// Pass to search service for neighbor lookup
            //if (_searchService is SearchService semanticSearch)
            //{
            //    semanticSearch.IndexChunksForNeighborLookup(chunks);
            //}

            var chunks = _vectorStoreService.GetChunks();

            _searchService.IndexChunksForNeighborLookup(chunks);
        }

        //private class ChunkMetadata
        //{
        //    public string Id { get; set; } = string.Empty;
        //    public string ToolName { get; set; } = string.Empty;
        //    public string DocumentType { get; set; } = string.Empty;
        //    public string FilePath { get; set; } = string.Empty;
        //    public string SectionHeading { get; set; } = string.Empty;
        //    public int ChunkIndex { get; set; }
        //    public int TokenCount { get; set; }
        //}
    }
}
