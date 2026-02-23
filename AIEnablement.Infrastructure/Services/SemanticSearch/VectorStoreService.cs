using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using System.Text.Json;

namespace AIEnablement.Infrastructure.Services.SemanticSearch
{
    public class VectorStoreService : IVectorStoreService
    {
        private readonly RagConfiguration _config;
        private readonly ILogger<VectorStoreService> _logger;
        private readonly InMemoryVectorStore _vectorStore;
        private InMemoryCollection<string, VectorRecord>? _collection;
        private readonly List<VectorRecord> _localCache = new();
        private const string CollectionName = "document_chunks";

        public VectorStoreService(
            RagConfiguration config,
            ILogger<VectorStoreService> logger)
        {
            _config = config;
            _logger = logger;
            _vectorStore = new InMemoryVectorStore();
        }

        public async Task AddChunksAsync(List<TextChunk> chunks)
        {
            if (_collection == null)
            {
                _collection = _vectorStore.GetCollection<string, VectorRecord>(CollectionName);

                await _collection.EnsureCollectionExistsAsync();
            }

            var records = chunks.Select(chunk => new VectorRecord
            {
                Id = chunk.Id,
                ToolName = chunk.ToolName,
                DocumentType = chunk.DocumentType,
                FilePath = chunk.FilePath,
                SectionHeading = chunk.SectionHeading,
                Content = chunk.Content,
                ChunkIndex = chunk.ChunkIndex,
                TokenCount = chunk.TokenCount,
                SectionId = chunk.SectionId,
                Embedding = chunk.Embedding
            }).ToList();

            foreach (var record in records)
            {
                await _collection.UpsertAsync(record);
            }

            foreach (var record in records)
            {
                var existingIndex = _localCache.FindIndex(x => x.Id == record.Id);
                if (existingIndex >= 0)
                {
                    _localCache[existingIndex] = record;
                }
                else
                {
                    _localCache.Add(record);
                }
            }

            _logger.LogInformation("Added {Count} chunks to vector store", chunks.Count);
        }

        public async Task<List<(TextChunk Chunk, double Score)>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int topK)
        {
            var searchResults = _collection.SearchAsync(
                queryEmbedding,
                topK
            );

            var results = new List<(TextChunk, double)>();

            await foreach (var result in searchResults)
            {
                if (result.Record != null)
                {
                    var chunk = new TextChunk
                    {
                        Id = result.Record.Id,
                        ToolName = result.Record.ToolName,
                        DocumentType = result.Record.DocumentType,
                        FilePath = result.Record.FilePath,
                        SectionHeading = result.Record.SectionHeading,
                        Content = result.Record.Content,
                        ChunkIndex = result.Record.ChunkIndex,
                        TokenCount = result.Record.TokenCount,
                        SectionId = result.Record.SectionId,
                        Embedding = result.Record.Embedding
                    };

                    results.Add((chunk, result.Score ?? 0));
                }
            }

            return results;
        }

        public List<TextChunk> GetChunks()
        {
            var chunks = _localCache.Select(vectorRecord => new TextChunk
            {
                Id = vectorRecord.Id,
                ToolName = vectorRecord.ToolName,
                DocumentType = vectorRecord.DocumentType,
                FilePath = vectorRecord.FilePath,
                SectionHeading = vectorRecord.SectionHeading,
                ChunkIndex = vectorRecord.ChunkIndex,
                Content = vectorRecord.Content,
                TokenCount = vectorRecord.TokenCount,
                SectionId = vectorRecord.SectionId
            }).ToList();

            return chunks;
        }

        public async Task SaveToDiskAsync()
        {
            var directory = _config.Ingestion.VectorStoreDirectory;
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, "vectorstore.json");

            if (_collection == null)
            {
                _logger.LogWarning("No collection to save");
                return;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, _localCache, options);

            _logger.LogInformation("Vector store saved to {Path} ({Count} records)",
                filePath,
                _localCache.Count);
        }

        public async Task LoadFromDiskAsync()
        {
            var filePath = Path.Combine(_config.Ingestion.VectorStoreDirectory, "vectorstore.json");

            if (!File.Exists(filePath))
            {
                _logger.LogInformation("No existing vector store found at {Path}", filePath);
                return;
            }

            _logger.LogInformation("Loading vector store from {Path}", filePath);

            _collection = _vectorStore.GetCollection<string, VectorRecord>(CollectionName);
            await _collection.EnsureCollectionExistsAsync();

            List<VectorRecord>? records;

            await using (var stream = File.OpenRead(filePath))
            {
                records = await JsonSerializer.DeserializeAsync<List<VectorRecord>>(stream);
            }

            if (records == null || records.Count == 0)
            {
                _logger.LogWarning("Vector store file empty");
                return;
            }

            _localCache.Clear();
            _localCache.AddRange(records);

            foreach (var record in records)
            {
                await _collection.UpsertAsync(record);
            }

            //var chunks = GetChunks();
            //var options = new JsonSerializerOptions
            //{
            //    WriteIndented = true
            //};
            //await using var stream1 = File.Create(Path.Combine(_config.Ingestion.VectorStoreDirectory, "chunks.json"));
            //await JsonSerializer.SerializeAsync(stream1, chunks, options);

            _logger.LogInformation("Loaded {Count} vectors from disk", records.Count);
        }
    }

    public class VectorRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;
        [VectorStoreData]
        public string ToolName { get; set; } = string.Empty;
        [VectorStoreData]
        public string DocumentType { get; set; } = string.Empty;
        [VectorStoreData]
        public string FilePath { get; set; } = string.Empty;
        [VectorStoreData]
        public string SectionHeading { get; set; } = string.Empty;
        [VectorStoreData]
        public string Content { get; set; } = string.Empty;
        [VectorStoreData]
        public int ChunkIndex { get; set; }
        [VectorStoreData]
        public int TokenCount { get; set; }
        [VectorStoreData]
        public string SectionId { get; set; } = string.Empty;
        [VectorStoreVector(1536)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
