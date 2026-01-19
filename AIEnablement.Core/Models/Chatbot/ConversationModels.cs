namespace AIEnablement.Core.Models.Chatbot;

public class ChatMessage
{
    public string Role { get; set; } = string.Empty; // "system", "user", "assistant", "tool"
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? ToolCallId { get; set; }
    public string? ToolName { get; set; }
}

public class ConversationSummary
{
    public int SystemMessages { get; set; }
    public int TotalMessages { get; set; }
    public int UserMessages { get; set; }
    public int AssistantMessages { get; set; }
    public int GetCurrentDateToolCalls { get; set; }
    public int AddDaysToDateToolCalls { get; set; }
    public DateTime ConversationStarted { get; set; }
    public TimeSpan Duration { get; set; }
}

public class ChatContent
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public enum ChatRole
{
    System,
    Assistant,
    User
}
