using AIEnablement.Business.Orchestrators.EvaluationQualityMetrics;
using AIEnablement.Business.Services.EvaluationQualityMetrics;
using AIEnablement.Business.Services.SemanticSearch;
using AIEnablement.Core.Interfaces.AgenticRAGAssistant;
using AIEnablement.Core.Interfaces.EvaluationQualityMetrics;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using AIEnablement.Core.Models.EvaluationQualityMetrics;
using AIEnablement.Core.Models.SemanticSearch;
using AIEnablement.Infrastructure.Plugins;
using AIEnablement.Infrastructure.Services;
using AIEnablement.Infrastructure.Services.AgenticRAGAssistant;
using AIEnablement.Infrastructure.Services.EvaluationQualityMetrics;
using AIEnablement.Infrastructure.Services.SemanticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIEnablement.EvaluationQualityMetrics.App;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                const string settingsFile = "appsettings.json";

                var config = RagChatbotConfiguration.Load(settingsFile);
                services.AddSingleton(config);

                var ragConfig = RagConfiguration.Load(settingsFile);
                services.AddSingleton(ragConfig);

                var evaluationConfig = EvaluationConfiguration.Load(settingsFile);
                services.AddSingleton(evaluationConfig);

                services.AddSingleton<IVectorStoreService, VectorStoreService>();
                services.AddSingleton<IEmbeddingService, EmbeddingService>();
                services.AddSingleton<ISearchService, SearchService>();
                services.AddSingleton<IPromptService, PromptService>();

                //Chatbot services
                services.AddSingleton<IAgenticRAGChatService, AgenticRAGChatService>();

                services.AddSingleton<DateToolsPlugin>();
                services.AddSingleton<RetrievalToolPlugin>();


                // Evaluation services
                services.AddSingleton<IDatasetService, DatasetService>();
                services.AddSingleton<IMetricsService, MetricsService>();
                services.AddSingleton<IExperimentRunner, ExperimentRunner>();

                //Orchestrator
                services.AddSingleton<EvaluationOrchestrator>();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        DisplayHelp();


        var orchestrator = host.Services.GetRequiredService<EvaluationOrchestrator>();

        await ConversationLoopAsync(orchestrator);

    }

    private static async Task ConversationLoopAsync(EvaluationOrchestrator orchestrator)
    {
        while (true)
        {
            var command = Console.ReadLine()?.Trim();

            switch (command)
            {
                case "load-dataset":
                    await orchestrator.LoadGoldenDatasetAsync();
                    break;

                case "run-experiments":
                    await orchestrator.RunExperimentsAsync();
                    break;

                case "--help":
                    DisplayHelp();
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    DisplayHelp();
                    break;
            }
        }
    }

    private static void DisplayHelp()
    {
        Console.WriteLine("RAG Evaluation Framework - Phase 5");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  load-dataset  - Load golden dataset");
        Console.WriteLine("  run-experiments   - Run evaluation experiments");
        Console.WriteLine("  --help           - Show help");
        Console.WriteLine();
    }
}