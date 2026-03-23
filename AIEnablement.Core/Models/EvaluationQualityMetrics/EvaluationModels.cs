using AIEnablement.Core.Models.SemanticSearch;

namespace AIEnablement.Core.Models.EvaluationQualityMetrics;

public class GoldenDataset
{
    public List<GoldenDataItem> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public bool Validated { get; set; }
}

public class GoldenDataItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Query { get; set; } = string.Empty;
    public string ReferenceAnswer { get; set; } = string.Empty;
    public List<string> RequiredFacts { get; set; } = [];
    public List<string> ExpectedSources { get; set; } = [];
    //public string ModelAnswer { get; set; } = string.Empty;
    //public List<ToolExecution> ToolExecutions { get; set; } = [];
    public bool ManuallyValidated { get; set; }
}

public class ToolExecution
{
    public string Role { get; set; } = string.Empty;
    public string? ToolName { get; set; }
    public string? Content { get; set; }
    public ToolCall? ToolCall { get; set; }
}

public class ToolResponse
{
    public string Content { get; set; } = string.Empty;
    public IReadOnlyCollection<RetrievalChunkSnapshot>? RetrievedChunks { get; set; }
}

public class ToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public class RetrievalChunkSnapshot
{
    public string DocumentId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public IReadOnlyCollection<string> NeighborBefore { get; set; } = new ArraySegment<string>();
    public IReadOnlyCollection<string> NeighborAfter { get; set; } = new ArraySegment<string>();
    public double RelevanceScore { get; set; }
}

public class ModelConfiguration
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

public class ModelResponse
{
    public string Query { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public List<ToolExecution> ToolExecutions { get; set; } = []; // RetrievedChunks
    public ModelConfiguration Configuration { get; set; } = new();
    public TimeSpan ResponseTime { get; set; }
}


// Evaluation Results
public class EvaluationResult
{
    public string QueryId { get; set; } = string.Empty;
    public string ConfigurationName { get; set; } = string.Empty;
    public ModelResponse RagResponse { get; set; } = new();
    public GoldenDataItem GoldenData { get; set; } = new();
    public MetricScores Metrics { get; set; } = new();
    public DateTime EvaluatedAt { get; set; }
}

public class MetricScores
{
    // Retrieval Metrics
    public double ContextualPrecision { get; set; }
    public string? ContextualPrecisionReason { get; set; }
    public double ContextualRecall { get; set; }
    public string? ContextualRecallReason { get; set; }
    public double ContextUtilization { get; set; }
    public string? ContextUtilizationReason { get; set; }

    // Answer Quality Metrics
    public double AnswerRelevancy { get; set; }
    public string? AnswerRelevancyReason { get; set; }
    public double Faithfulness { get; set; }
    public string? FaithfulnessReason { get; set; }
    public double AnswerCorrectness { get; set; }
    public string? AnswerCorrectnessReason { get; set; }

    // Analysis
    public List<string> FalsePositives { get; set; } = new(); // Irrelevant chunks retrieved
    public List<string> FalseNegatives { get; set; } = new(); // Important chunks missed
    public List<string> Issues { get; set; } = new();
}


// Experiment Results
public class ExperimentResults
{
    public string ExperimentName { get; set; } = string.Empty;
    public DateTime RunAt { get; set; }
    public List<ConfigurationResults> ConfigurationResults { get; set; } = new();
}

public class ConfigurationResults
{
    public ModelConfiguration Configuration { get; set; } = new();
    public List<EvaluationResult> Results { get; set; } = new();
    public AggregateMetrics AggregateMetrics { get; set; } = new();
    public PerformanceMetrics Performance { get; set; } = new();
}

public class AggregateMetrics
{
    public double AvgContextualPrecision { get; set; }
    public double AvgContextualRecall { get; set; }
    public double AvgContextUtilization { get; set; }
    public double AvgAnswerRelevancy { get; set; }
    public double AvgFaithfulness { get; set; }
    public double AvgAnswerCorrectness { get; set; }
    public int TotalFalsePositives { get; set; }
    public int TotalFalseNegatives { get; set; }
}

public class PerformanceMetrics
{
    public TimeSpan AvgResponseTime { get; set; }
}


public class EvaluationMetricResult
{
    public string Reason { get; set; } = string.Empty;
    public double Score { get; set; }
}
public class ContextualPrecision : EvaluationMetricResult
{
    public int RelevantCount { get; set; }
    public int TotalCount { get; set; }
}

public class ContextualRecall : EvaluationMetricResult
{
    public int FoundCount { get; set; }
    public int TotalNeeded { get; set; }
}

public class ContextUtilization : EvaluationMetricResult
{
    public int UtilizedChunks { get; set; }
    public int TotalChunks { get; set; }
}

public class RetrievalErrors
{
    public List<string> FalsePositives { get; set; } = [];
    public List<string> FalseNegatives { get; set; } = [];
}

public class RetrievalMetrics
{
    public ContextualPrecision ContextualPrecision { get; set; } = new();
    public ContextualRecall ContextualRecall { get; set; } = new();
    public ContextUtilization ContextUtilization { get; set; } = new();
    public RetrievalErrors RetrievalErrors { get; set; } = new();
}

public class AnswerRelevancy : EvaluationMetricResult
{
}

public class Faithfulness : EvaluationMetricResult
{
    public int SupportedClaims { get; set; }
    public int TotalClaims { get; set; }
    public string[] Hallucinations { get; set; } = [];
}

public class AnswerCorrectness : EvaluationMetricResult
{
    public string[] MissingInfo { get; set; } = [];
    public string[] IncorrectInfo { get; set; } = [];
}

public class AnswerQuality
{
    public AnswerRelevancy AnswerRelevancy { get; set; } = new();
    public Faithfulness Faithfulness { get; set; } = new();
    public AnswerCorrectness AnswerCorrectness { get; set; } = new();
}