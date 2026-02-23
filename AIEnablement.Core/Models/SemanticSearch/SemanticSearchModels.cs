namespace AIEnablement.Core.Models.SemanticSearch
{
    public class DocumentMetadata
    {
        public string ToolName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SectionId { get; set; } = string.Empty;
    }

    public class ParsedSection
    {
        public string SectionId { get; set; } = string.Empty;
        public string Heading { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class ParsedDocument
    {
        public DocumentMetadata Metadata { get; set; } = new();
        public List<ParsedSection> Sections { get; set; } = new();
    }

    public class TextChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ToolName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string SectionHeading { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int ChunkIndex { get; set; }
        public int TokenCount { get; set; }
        public string SectionId { get; set; } = string.Empty;
        public ReadOnlyMemory<float> Embedding { get; set; }
    }

    public class SearchResult
    {
        public TextChunk Chunk { get; set; } = new();
        public double RelevanceScore { get; set; }
        public List<TextChunk> NeighborChunks { get; set; } = new();
    }

    public class SearchResponse
    {
        public string Query { get; set; } = string.Empty;
        public List<SearchResult> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public TimeSpan SearchDuration { get; set; }
    }
}
