using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Interfaces.SemanticSearch
{
    public interface IVectorStoreService
    {
        Task AddChunksAsync(List<TextChunk> chunks);
        Task<List<(TextChunk Chunk, double Score)>> SearchAsync(ReadOnlyMemory<float> queryEmbedding, int topK);
        Task SaveToDiskAsync();
        Task LoadFromDiskAsync();
        Task<TextChunk?> GetChunkByIndexAsync(string toolName, string documentType, int chunkIndex);
        List<TextChunk> GetChunks();
    }
}
