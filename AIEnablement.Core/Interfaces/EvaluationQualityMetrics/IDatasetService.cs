using AIEnablement.Core.Models.EvaluationQualityMetrics;

namespace AIEnablement.Core.Interfaces.EvaluationQualityMetrics;

public interface IDatasetService
{
    //Task<GoldenDataset> GenerateInitialDatasetAsync();
    Task LoadDatasetAsync(string path);
    GoldenDataset GetDataset();
    //Task SaveDatasetAsync(GoldenDataset dataset, string path);
    //Task<bool> ValidateDatasetAsync(GoldenDataset dataset);
}
