using System.Text.Json;

namespace AIEnablement.Core.Models;

public class AppConfiguration
{
    public GenerationSettings Generation { get; set; } = new();
    public Dictionary<string, ModelSettings> Models { get; set; } = new();
    public ApiKeysSettings ApiKeys { get; set; } = new();
    public ApiEndpointSettings ApiEndpoint { get; set; } = new();
    public DataQualitySettings DataQuality { get; set; } = new();
    public PromptsSettings Prompts { get; set; } = new();

    public static AppConfiguration Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load configuration");
    }
}

public class GenerationSettings
{
    public int NumberOfTools { get; set; }
    public int DocumentsPerTool { get; set; }
    public string OutputDirectory { get; set; } = "data";
    public List<string> AvailableDocumentTypes { get; set; } = new();
}

public class ModelSettings
{
    public string ModelId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public double TopP { get; set; }
}

public class ApiKeysSettings
{
    public string OpenAI { get; set; } = string.Empty;
}

public class ApiEndpointSettings
{
    public string Endpoint { get; set; } = string.Empty;
}

public class DataQualitySettings
{
    public int MinIssuesPerDocument { get; set; }
    public int MaxIssuesPerDocument { get; set; }
}

public class PromptsSettings
{
    public string ToolIdeationPromptPath { get; set; } = string.Empty;
    public string TocGenerationPromptPath { get; set; } = string.Empty;
    public string SectionGenerationPromptPath { get; set; } = string.Empty;
}
