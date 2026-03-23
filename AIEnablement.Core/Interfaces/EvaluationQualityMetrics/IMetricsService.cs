using AIEnablement.Core.Models.EvaluationQualityMetrics;

namespace AIEnablement.Core.Interfaces.EvaluationQualityMetrics;

public interface IMetricsService
{
    Task<MetricScores> EvaluateAsync(ModelResponse response, GoldenDataItem goldenData);
}
