using System.Text.Json;

namespace AIEnablement.Core.Models.EvaluationQualityMetrics;

public class EvaluationConfiguration
{
    public ApiKeysSettings ApiKeys { get; set; } = new();
    //public RagChatbotSettings RagChatbot { get; set; } = new();
    public DatasetSettings Dataset { get; set; } = new();
    public ExperimentsSettings Experiments { get; set; } = new();
    public MetricsSettings Metrics { get; set; } = new();
    public EvaluationPromptPaths EvaluationPromptPaths { get; set; } = new();

    public static EvaluationConfiguration Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EvaluationConfiguration>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to load configuration");
    }
}

public class DatasetSettings
{
    public string GoldenDatasetPath { get; set; } = string.Empty;
}

public class ExperimentsSettings
{
    public string OutputDirectory { get; set; } = string.Empty;
    public List<ConfigurationDefinition> Configurations { get; set; } = new();
}

public class EvaluationPromptPaths
{
    public string RetrievalMetricsPromptPath { get; set; } = string.Empty;
    public string AnswerQualityPromptPath { get; set; } = string.Empty;
}

public class ConfigurationDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double TopP { get; set; }
    public int TopK { get; set; }
    public double MinRelevanceScore { get; set; }
    public bool IncludeNeighbors { get; set; }
}

public class MetricsSettings
{
    public string EvaluatorModel { get; set; } = string.Empty;
    public double EvaluatorTemperature { get; set; }
    public double EvaluatorTopP { get; set; }
}