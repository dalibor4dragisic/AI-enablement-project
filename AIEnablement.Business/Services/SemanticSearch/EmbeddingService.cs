using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AIEnablement.Business.Services.SemanticSearch
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingService;
        private readonly RagConfiguration _config;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(
            RagConfiguration config,
            ILogger<EmbeddingService> logger)
        {
            _config = config;
            _logger = logger;

            var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            builder.AddAzureOpenAIEmbeddingGenerator(
                deploymentName: config.Embedding.ModelId,
                endpoint: config.ApiEndpoint.Endpoint,
                modelId: config.Embedding.ModelId,
                apiKey: config.ApiKeys.OpenAI,
                dimensions: config.Embedding.Dimensions
            );
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            var kernel = builder.Build();
            _embeddingService = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }

        public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var embedding = await _embeddingService.GenerateAsync(text);
                return embedding.Vector;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text: {Text}", text.Substring(0, Math.Min(50, text.Length)));
                throw;
            }
        }

        public async Task<List<ReadOnlyMemory<float>>> GenerateEmbeddingsBatchAsync(List<string> texts)
        {
            var embeddings = new List<ReadOnlyMemory<float>>();

            // Process in batches
            for (int i = 0; i < texts.Count; i += _config.Embedding.BatchSize)
            {
                var batch = texts.Skip(i).Take(_config.Embedding.BatchSize).ToList();

                _logger.LogInformation("Generating embeddings for batch {Current}/{Total}",
                    i / _config.Embedding.BatchSize + 1,
                    (texts.Count + _config.Embedding.BatchSize - 1) / _config.Embedding.BatchSize);

                try
                {
                    var batchEmbeddings = await _embeddingService.GenerateAsync(batch);
                    embeddings.AddRange(batchEmbeddings.Select(embedding => embedding.Vector));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating embeddings for batch starting at index {Index}", i);
                    throw;
                }

                // Small delay to avoid rate limiting
                if (i + _config.Embedding.BatchSize < texts.Count)
                {
                    await Task.Delay(100);
                }
            }

            return embeddings;
        }
    }
}
