using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.AgenticRAGAssistant;

public interface IAgenticRAGChatService
{
    Task<string> GetResponseAsync(string message);
    Task<ConversationSummary> GetSummary();
    IReadOnlyCollection<ChatContent> GetAllMessages();
}
