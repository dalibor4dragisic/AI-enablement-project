using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.Chatbot;

public interface IConversationManager
{
    void AddSystemMessage(string content);
    void AddUserMessage(string content);
    void AddAssistantMessage(string content);
    List<ChatContent> GetChatHistory(); //ChatHistory GetChatHistory();
    ConversationSummary GetSummary();
    List<ChatContent> GetAllMessages();
    void TrimHistory();
    void IncrementGetCurrentDatePluginCall();
    void IncrementAddDaysToDatePluginCall();
}
