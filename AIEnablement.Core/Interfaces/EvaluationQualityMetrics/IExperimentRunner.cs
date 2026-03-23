using AIEnablement.Core.Models.EvaluationQualityMetrics;

namespace AIEnablement.Core.Interfaces.EvaluationQualityMetrics;

public interface IExperimentRunner
{
    Task<ExperimentResults> RunExperimentsAsync(GoldenDataset dataset);
}