using System.Text.Json;

namespace AIEnablement.Core.Models.SemanticSearch;

public class RagConfiguration
{
    public ApiKeysSettings ApiKeys { get; set; } = new();
    public ApiEndpointSettings ApiEndpoint { get; set; } = new();
    public IngestionSettings Ingestion { get; set; } = new();
    public EmbeddingSettings Embedding { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
    public SearchSettings Search { get; set; } = new();

    public static RagConfiguration Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<RagConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load configuration");
    }
}

public class ApiKeysSettings
{
    public string OpenAI { get; set; } = string.Empty;
}

public class ApiEndpointSettings
{
    public string Endpoint { get; set; } = string.Empty;
}

public class IngestionSettings
{
    public string InputDirectory { get; set; } = string.Empty;
    public string VectorStoreDirectory { get; set; } = string.Empty;
}

public class EmbeddingSettings
{
    public string ModelId { get; set; } = string.Empty;
    public int Dimensions { get; set; }
    public int BatchSize { get; set; }
}

public class ChunkingSettings
{
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public int MinChunkSize { get; set; }
    public bool StructureAware { get; set; }
    //public bool PreserveHeadings { get; set; }
}

public class SearchSettings
{
    public int TopK { get; set; }
    public bool IncludeNeighbors { get; set; }
    public int NeighborsBefore { get; set; }
    public int NeighborsAfter { get; set; }
    public double MinRelevanceScore { get; set; }
}
