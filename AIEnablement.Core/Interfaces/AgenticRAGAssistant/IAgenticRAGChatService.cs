using AIEnablement.Core.Models.Chatbot;
using AIEnablement.Core.Models.EvaluationQualityMetrics;

namespace AIEnablement.Core.Interfaces.AgenticRAGAssistant;

public interface IAgenticRAGChatService
{
    Task<string> GetResponseAsync(string message);
    Task<ModelResponse> GetResponseAsync(string message, ModelConfiguration configuration);
    Task<ConversationSummary> GetSummary();
    IReadOnlyCollection<ChatContent> GetAllMessages();
    GoldenDataset CreateGoldenDatasetFromHistory();
}
