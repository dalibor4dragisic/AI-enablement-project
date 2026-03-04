using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace AIEnablement.Infrastructure.Plugins
{

    public class RetrievalToolPlugin
    {
        private readonly ISearchService _searchService;
        private readonly RagChatbotConfiguration _config;
        private readonly ILogger<RetrievalToolPlugin> _logger;

        public RetrievalToolPlugin(
            ISearchService searchService,
            RagChatbotConfiguration config,
            ILogger<RetrievalToolPlugin> logger)
        {
            _searchService = searchService;
            _config = config;
            _logger = logger;
        }

        [KernelFunction("search_documents")]
        [Description("Search through privacy policies, terms of service, and other legal documents. " +
                     "Use this when the user asks about specific policies, legal terms, data handling, " +
                     "security practices, or any information that would be in official documentation. " +
                     "Returns relevant excerpts from the documents with source information.")]
        public async Task<string> SearchDocuments(
            [Description("The search query. Be specific and include key terms from the user's question.")]
        string query,

            [Description("Optional: Filter by tool/product name (e.g., 'CloudVault Pro', 'StreamlineBoard'). " +
                     "Leave empty to search all documents.")]
        string? toolName = null,

            [Description("Number of results to return (1-10). Default is 5.")]
        int topK = 5)
        {
            //_logger.LogInformation("Retrieval tool called: query='{Query}', tool='{Tool}', topK={TopK}",
            //    query, toolName ?? "all", topK);

            try
            {
                topK = Math.Clamp(topK, 1, _config.Retrieval.MaxTopK);

                var searchResults = await _searchService.SearchAsync(query, topK * 2);// Get more, then filter

                //Apply filters
                var filteredResults = searchResults.Results
                    .Where(r => string.IsNullOrEmpty(toolName) ||
                               r.Chunk.ToolName.Replace("_", " ").Contains(toolName, StringComparison.OrdinalIgnoreCase))
                    .Take(topK)
                    .ToList();

                if (!filteredResults.Any())
                {
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        message = "No relevant documents found for this query.",
                        results = Array.Empty<object>()
                    });
                }

                //Format results for LLM
                var formattedResults = filteredResults.Select((r, index) => new
                {
                    result_number = index + 1,
                    relevance_score = Math.Round(r.RelevanceScore, 3),
                    source = new
                    {
                        tool_name = r.Chunk.ToolName,
                        document_type = r.Chunk.DocumentType,
                        section_heading = r.Chunk.SectionHeading,
                        file_path = r.Chunk.FilePath
                    },
                    content = r.Chunk.Content,
                    neighbor_context = r.NeighborChunks.Any()
                        ? new
                        {
                            chunks_before = r.NeighborChunks
                                .Where(n => n.ChunkIndex < r.Chunk.ChunkIndex)
                                .Select(n => n.Content)
                                .ToList(),
                            chunks_after = r.NeighborChunks
                                .Where(n => n.ChunkIndex > r.Chunk.ChunkIndex)
                                .Select(n => n.Content)
                                .ToList()
                        }
                        : null
                }).ToList();

                var response = new
                {
                    success = true,
                    query = query,
                    total_results = formattedResults.Count,
                    results = formattedResults
                };

                //_logger.LogInformation("Retrieved {Count} results with avg score {AvgScore:F3}",
                //    filteredResults.Count,
                //    filteredResults.Average(r => r.RelevanceScore));

                return JsonSerializer.Serialize(response, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in retrieval tool");

                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Error performing search: {ex.Message}",
                    results = Array.Empty<object>()
                });
            }
        }
    }
}
