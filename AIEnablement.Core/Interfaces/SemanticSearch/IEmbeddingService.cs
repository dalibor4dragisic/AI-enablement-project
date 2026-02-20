namespace AIEnablement.Core.Interfaces.SemanticSearch
{
    public interface IEmbeddingService
    {
        Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text);
        Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(List<string> texts);
    }
}
