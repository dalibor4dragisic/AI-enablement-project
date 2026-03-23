using AIEnablement.Core.Interfaces.EvaluationQualityMetrics;
using AIEnablement.Core.Models.EvaluationQualityMetrics;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIEnablement.Infrastructure.Services.EvaluationQualityMetrics;

public class DatasetService : IDatasetService
{
    private readonly ILogger<DatasetService> _logger;

    private GoldenDataset _goldenDataset;

    public DatasetService(
        ILogger<DatasetService> logger)
    {
        _logger = logger;
    }

    public async Task LoadDatasetAsync(string path)
    {

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Dataset not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);
        var dataset = JsonSerializer.Deserialize<GoldenDataset>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize dataset");

        _logger.LogInformation("Loaded dataset with {Count} items from {Path}",
            dataset.Items.Count, path);

        _goldenDataset = dataset;
    }

    public GoldenDataset GetDataset()
    {
        return _goldenDataset;
    }
}
