using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Business.Orchestrators.SemanticSearch;

public class IngestionOrchestrator
{
    private readonly IHtmlParsingService _parsingService;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ISearchService _searchService;
    private readonly RagConfiguration _config;
    private readonly ILogger<IngestionOrchestrator> _logger;

    public IngestionOrchestrator(
        IHtmlParsingService parsingService,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        IVectorStoreService vectorStoreService,
        ISearchService searchService,
        RagConfiguration config,
        ILogger<IngestionOrchestrator> logger)
    {
        _parsingService = parsingService;
        _chunkingService = chunkingService;
        _embeddingService = embeddingService;
        _vectorStoreService = vectorStoreService;
        _searchService = searchService;
        _config = config;
        _logger = logger;
    }

    public async Task IngestDocumentsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("           RAG INGESTION - Phase 3                        ");
        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine();

        var inputDir = _config.Ingestion.InputDirectory;

        if (!Directory.Exists(inputDir))
        {
            _logger.LogError("Input directory not found: {Dir}", inputDir);
            Console.WriteLine($"❌ Error: Input directory not found: {inputDir}");
            Console.WriteLine("   Make sure to run Phase 1 document generation first.");
            return;
        }

        // Find all HTML files
        var htmlFiles = Directory.GetFiles(inputDir, "*.html", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("toc_"))
            .ToList();

        _logger.LogInformation("Found {Count} HTML documents", htmlFiles.Count);
        Console.WriteLine($"📄 Found {htmlFiles.Count} HTML documents");
        Console.WriteLine();

        var allChunks = new List<TextChunk>();
        int processedDocs = 0;

        var parsedDocuments = new List<ParsedDocument?>();

        foreach (var htmlFile in htmlFiles)
        {
            processedDocs++;
            var relativePath = Path.GetRelativePath(inputDir, htmlFile);
            Console.WriteLine($"[{processedDocs}/{htmlFiles.Count}] Processing: {relativePath}");

            try
            {
                // Parse HTML
                Console.Write("  ↳ Parsing HTML... ");
                var document = await _parsingService.ParseHtmlDocument(htmlFile);
                Console.WriteLine($"✓ ({document.Sections.Count} sections)");

                parsedDocuments.Add(document);

                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {File}", htmlFile);
                Console.WriteLine($"  ✗ Error: {ex.Message}");
            }
        }

        parsedDocuments = parsedDocuments.OrderBy(document => document.Metadata.SectionId).ToList();

        foreach (var document in parsedDocuments)
        {
            // Chunk document
            Console.Write("  ↳ Chunking... ");
            var chunks = _chunkingService.ChunkDocument(document);
            //Console.WriteLine($"✓ ({chunks.Count} chunks)");

            allChunks.AddRange(chunks);
        }

        Console.WriteLine();
        Console.WriteLine($"📊 Total chunks created: {allChunks.Count}");
        Console.WriteLine($"   Average tokens per chunk: {(allChunks.Any() ? allChunks.Average(c => c.TokenCount) : 0):F1}");
        Console.WriteLine();

        // Generate embeddings
        Console.WriteLine("🔮 Generating embeddings...");
        var texts = allChunks.Select(c => c.Content).ToList();
        var embeddings = await _embeddingService.GenerateEmbeddingsBatchAsync(texts);

        // Assign embeddings to chunks
        for (int i = 0; i < allChunks.Count; i++)
        {
            allChunks[i].Embedding = embeddings[i];
        }

        Console.WriteLine($"   ✓ Generated {embeddings.Count} embeddings");
        Console.WriteLine();

        // Store in vector database
        Console.WriteLine("💾 Storing in vector database...");
        await _vectorStoreService.AddChunksAsync(allChunks);
        Console.WriteLine("   ✓ Chunks indexed");
        Console.WriteLine();

        // Save to disk
        Console.WriteLine("💿 Persisting to disk...");
        await _vectorStoreService.SaveToDiskAsync();

        // Save chunk metadata for neighbor lookup
        //await SaveChunkMetadataAsync(allChunks);

        Console.WriteLine($"   ✓ Saved to {_config.Ingestion.VectorStoreDirectory}");
        Console.WriteLine();

        Console.WriteLine("═══════════════════════════════════════════════════════════");
        Console.WriteLine("✅ INGESTION COMPLETE");
        Console.WriteLine($"   • Documents processed: {processedDocs}");
        Console.WriteLine($"   • Total chunks indexed: {allChunks.Count}");
        Console.WriteLine($"   • Vector store: {_config.Ingestion.VectorStoreDirectory}");
        Console.WriteLine("═══════════════════════════════════════════════════════════");


        //var response = await _searchService.SearchAsync("What are security benefits in DataForge Analytics?");
    }

    private async Task SaveChunkMetadataAsync(List<TextChunk> chunks)
    {
        var directory = _config.Ingestion.VectorStoreDirectory;
        Directory.CreateDirectory(directory);

        var metadataPath = Path.Combine(directory, "chunk_metadata.json");

        var metadata = chunks.Select(c => new
        {
            c.Id,
            c.ToolName,
            c.DocumentType,
            c.FilePath,
            c.SectionHeading,
            c.ChunkIndex,
            c.TokenCount
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(metadataPath, json);
        _logger.LogInformation("Saved chunk metadata to {Path}", metadataPath);
    }
}