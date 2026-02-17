using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.Chatbot;

public interface ILLMChatService
{
    Task<string> GetResponseAsync(string message);
    Task<ConversationSummary> GetSummary();
    IReadOnlyCollection<ChatContent> GetAllMessages();
}
