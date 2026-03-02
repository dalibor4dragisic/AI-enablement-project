using AIEnablement.Core.Models.Chatbot;
using AIEnablement.Core.Models.SemanticSearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIEnablement.Core.Models.AgenticRAGAssistant
{
    public class RagChatbotConfiguration
    {
        public ApiKeysSettings ApiKeys { get; set; } = new();
        public ApiEndpointSettings ApiEndpoint { get; set; } = new();
        public ModelSettings Model { get; set; } = new();
        public AssistantSettings Assistant { get; set; } = new();
        public RetrievalSettings Retrieval { get; set; } = new();
        public PromptsSettings Prompts { get; set; } = new();

        public ChunkingSettings Chunking { get; set; } = new();
        public Dictionary<string, ToolSettings> Tools { get; set; } = new();

        public static RagChatbotConfiguration Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<RagChatbotConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to load configuration");
        }
    }

    public class ApiKeysSettings
    {
        public string OpenAI { get; set; } = string.Empty;
    }

    public class ModelSettings
    {
        public string ModelId { get; set; } = string.Empty;
        public string ModelLightId { get; set; } = string.Empty;
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public double TopP { get; set; }
        public int MaxInputTokens { get; set; }
    }

    public class AssistantSettings
    {
        public string Name { get; set; } = string.Empty;
        public string PersonalityPromptPath { get; set; } = string.Empty;
        public bool EnableRetrieval { get; set; }
        public int MaxHistoryMessages { get; set; }
        public double SummarizationThreshold { get; set; }
        public bool EnableTools { get; set; }
        public string SummaryPreservePromptPath { get; set; } = string.Empty;
    }

    public class RetrievalSettings
    {
        public string VectorStoreDirectory { get; set; } = string.Empty;
        public string EmbeddingModelId { get; set; } = string.Empty;
        public int DefaultTopK { get; set; }
        public double MinRelevanceScore { get; set; }
        public bool IncludeNeighbors { get; set; }
        public int MaxChunksInContext { get; set; }
    }

    public class PromptsSettings
    {
        public string ConversationSummaryPromptPath { get; set; } = string.Empty;
        public string CitationPromptPath { get; set; } = string.Empty;
    }
}
