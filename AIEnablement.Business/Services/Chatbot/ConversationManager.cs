using AIEnablement.Core.Interfaces.Chatbot;
using AIEnablement.Core.Models.Chatbot;

namespace AIEnablement.Business.Services.Chatbot;

public class ConversationManager : IConversationManager
{
    //private readonly List<ChatMessage> _messages = new();
    private readonly List<ChatContent> _messages = new();
    private readonly ChatbotConfiguration _config;
    private readonly DateTime _conversationStarted;
    private int _getCurrentDatePluginCalls;
    private int _addDaysToDatePluginCalls;

    public ConversationManager(ChatbotConfiguration config)
    {
        _config = config;
        _conversationStarted = DateTime.Now;
    }

    public void AddSystemMessage(string content)
    {
        _messages.Add(new ChatContent
        {
            Role = ChatRole.System,
            Content = content
        });
    }

    public void AddUserMessage(string content)
    {
        _messages.Add(new ChatContent
        {
            Role = ChatRole.User,
            Content = content
        });
    }

    public void AddAssistantMessage(string content)
    {
        _messages.Add(new ChatContent
        {
            Role = ChatRole.Assistant,
            Content = content
        });
    }

    //public void AddToolMessage(string toolCallId, string toolName, string content)
    //{
    //    _messages.Add(new ChatMessage
    //    {
    //        Role = "tool",
    //        Content = content,
    //        Timestamp = DateTime.Now,
    //        ToolCallId = toolCallId,
    //        ToolName = toolName
    //    });
    //}

    public List<ChatContent> GetChatHistory() //public ChatHistory GetChatHistory()
    {
        //var history = new ChatHistory();

        //foreach (var message in _messages)
        //{
        //    switch (message.Role.ToLower())
        //    {
        //        case "system":
        //            history.AddSystemMessage(message.Content);
        //            break;
        //        case "user":
        //            history.AddUserMessage(message.Content);
        //            break;
        //        case "assistant":
        //            history.AddAssistantMessage(message.Content);
        //            break;
        //    }
        //}

        return _messages;
    }

    public ConversationSummary GetSummary()
    {
        var userMessages = _messages.Count(chatContent => chatContent.Role == ChatRole.User);
        var assistantMessages = _messages.Count(chatContent => chatContent.Role == ChatRole.Assistant);
        var systemMessages = _messages.Count(chatContent => chatContent.Role == ChatRole.System);

        return new ConversationSummary
        {
            TotalMessages = _messages.Count,
            UserMessages = userMessages,
            SystemMessages = systemMessages,
            AssistantMessages = assistantMessages,
            GetCurrentDateToolCalls = _getCurrentDatePluginCalls,
            AddDaysToDateToolCalls = _addDaysToDatePluginCalls,
            ConversationStarted = _conversationStarted,
            Duration = DateTime.Now - _conversationStarted
        };
    }

    public List<ChatContent> GetAllMessages()
    {
        return new List<ChatContent>(_messages);
    }

    public void TrimHistory()
    {
        if (_messages.Count <= _config.Assistant.MaxHistoryMessages)
            return;

        // Keep system message and recent messages
        var systemMessages = _messages.Where(m => m.Role == ChatRole.System).ToList();
        var recentMessages = _messages
            .Where(m => m.Role != ChatRole.System)
            .TakeLast(_config.Assistant.MaxHistoryMessages - systemMessages.Count)
            .ToList();

        _messages.Clear();
        _messages.AddRange(systemMessages);
        _messages.AddRange(recentMessages);
    }

    public void IncrementGetCurrentDatePluginCall()
    {
        _getCurrentDatePluginCalls++;
    }

    public void IncrementAddDaysToDatePluginCall()
    {
        _addDaysToDatePluginCalls++;
    }
}
