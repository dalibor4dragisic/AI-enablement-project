using AIEnablement.Business.Orchestrators.SemanticSearch;
using AIEnablement.Business.Services.SemanticSearch;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

public class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var config = RagConfiguration.Load("appsettings.json");
                services.AddSingleton(config);

                services.AddInMemoryVectorStore();

                services.AddSingleton<IHtmlParsingService, HtmlParsingService>();
                services.AddSingleton<IChunkingService, ChunkingService>();
                services.AddSingleton<IEmbeddingService, EmbeddingService>();
                services.AddSingleton<IVectorStoreService, VectorStoreService>();
                services.AddSingleton<ISearchService, SearchService>();
                services.AddSingleton<IngestionOrchestrator>();
                services.AddSingleton<SearchOrchestrator>();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        //if (args.Length == 0 || args[0] == "--help")
        //{
        //    DisplayHelp();
        //    return;
        //}

        DisplayHelp();

        var command= Console.ReadLine()?.Trim();

        //var ingestionOrchestrator = host.Services.GetRequiredService<IngestionOrchestrator>();
        //await ingestionOrchestrator.IngestDocumentsAsync();

        //var command = args[0].ToLower();

        //var command = "search";//ingest;

        switch (command)
        {
            case "ingest":
                var ingestionOrchestrator = host.Services.GetRequiredService<IngestionOrchestrator>();
                await ingestionOrchestrator.IngestDocumentsAsync();
                break;

            case "search":
                var searchOrchestrator = host.Services.GetRequiredService<SearchOrchestrator>();
                await searchOrchestrator.RunInteractiveSearchAsync();
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

    static void DisplayHelp()
    {
        Console.WriteLine("RAG Console Application - Phase 3");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run ingest   - Index HTML documents from Phase 1");
        Console.WriteLine("  dotnet run search   - Interactive semantic search interface");
        Console.WriteLine("  dotnet run --help   - Display this help message");
        Console.WriteLine();
        Console.Write("Command: ");
    }
}
