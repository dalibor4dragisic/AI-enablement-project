using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Core.Interfaces.Chatbot;

public interface IChatManager
{
    void AddSystemMessage(string content);
    void AddUserMessage(string content);
    void AddAssistantMessage(string content);
    ConversationSummary GetSummary();
    IReadOnlyCollection<ChatContent> GetAllMessages();
    void TrimHistory();
    void IncrementGetCurrentDatePluginCall();
    void IncrementAddDaysToDatePluginCall();
}
