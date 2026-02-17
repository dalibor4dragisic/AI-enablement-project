using System.Text.Json;

namespace AIEnablement.Core.Models.Chatbot;

public class ChatbotConfiguration
{
    public ApiKeysSettings ApiKeys { get; set; } = new();
    public ApiEndpointSettings ApiEndpoint { get; set; } = new();
    public ModelSettings Model { get; set; } = new();
    public AssistantSettings Assistant { get; set; } = new();
    public Dictionary<string, ToolSettings> Tools { get; set; } = new();

    public static ChatbotConfiguration Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ChatbotConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load configuration");
    }
}

public class ApiKeysSettings
{
    public string OpenAI { get; set; } = string.Empty;
}

public class ApiEndpointSettings
{
    public string Endpoint { get; set; } = string.Empty;
}

public class ModelSettings
{
    public string ModelId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public double TopP { get; set; }
}

public class AssistantSettings
{
    public string Name { get; set; } = string.Empty;
    public string PersonalityPromptPath { get; set; } = string.Empty;
    public string SummaryPreservePromptPath { get; set; } = string.Empty;
    public bool EnableTools { get; set; }
    public int MaxHistoryMessages { get; set; }
}

public class ToolSettings
{
    public bool Enabled { get; set; }
}
