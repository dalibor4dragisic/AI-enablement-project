using AIEnablement.Core.Models.EvaluationQualityMetrics;
using Microsoft.Extensions.Logging;
using AIEnablement.Core.Interfaces.EvaluationQualityMetrics;
using AIEnablement.Core.Interfaces.SemanticSearch;

namespace AIEnablement.Business.Orchestrators.EvaluationQualityMetrics;

public class EvaluationOrchestrator
{
    private readonly IDatasetService _datasetService;
    private readonly IExperimentRunner _experimentRunner;
    private readonly IVectorStoreService _vectorStoreService;
    private readonly ISearchService _searchService;
    private readonly EvaluationConfiguration _config;
    private readonly ILogger<EvaluationOrchestrator> _logger;

    public EvaluationOrchestrator(
        IDatasetService datasetService,
        IExperimentRunner experimentRunner,
        EvaluationConfiguration config,
        ILogger<EvaluationOrchestrator> logger, 
        IVectorStoreService vectorStoreService, 
        ISearchService searchService)
    {
        _datasetService = datasetService;
        _experimentRunner = experimentRunner;
        _config = config;
        _logger = logger;
        _vectorStoreService = vectorStoreService;
        _searchService = searchService;
    }

    public async Task LoadGoldenDatasetAsync()
    {
        Console.WriteLine("-------------------------------------------");
        Console.WriteLine("   Golden Dataset Generation");
        Console.WriteLine("-------------------------------------------");
        Console.WriteLine();

        await _datasetService.LoadDatasetAsync(_config.Dataset.GoldenDatasetPath);

        var dataset = _datasetService.GetDataset();


        Console.WriteLine($"Loaded {dataset.Items.Count} dataset items");
        Console.WriteLine($"Loaded from: {_config.Dataset.GoldenDatasetPath}");
        Console.WriteLine();

        await _vectorStoreService.LoadFromDiskAsync();
        LoadChunkMetadataAsync();
    }

    public async Task RunExperimentsAsync()
    {
        Console.WriteLine("-------------------------------------------");
        Console.WriteLine("   Running Experiments");
        Console.WriteLine("-------------------------------------------");
        Console.WriteLine();

        var dataset = _datasetService.GetDataset();

        if (!dataset.Validated)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning: Dataset not validated.");
            Console.ResetColor();
        }

        Console.WriteLine($"Testing {_config.Experiments.Configurations.Count} configurations:");
        foreach (var config in _config.Experiments.Configurations)
        {
            Console.WriteLine($"  - {config.Name}: {config.Description}");
        }
        Console.WriteLine();

        // Run experiments
        await _experimentRunner.RunExperimentsAsync(dataset);

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Experiments complete!");
        Console.ResetColor();
        Console.WriteLine($"  Results saved to: {_config.Experiments.OutputDirectory}");
    }

    private void LoadChunkMetadataAsync()
    {
        var chunks = _vectorStoreService.GetChunks();

        _searchService.IndexChunksForNeighborLookup(chunks);
    }
}
