using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.Chatbot;

public interface ILLMChatService
{
    Task<string> GetResponseAsync(List<ChatContent> chatHistoryContent);
}
