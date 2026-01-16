using AIEnablement.Business.Services.DocumentGeneration;
using AIEnablement.Core.Interfaces.DocumentGeneration;
using AIEnablement.Core.Models;
using AIEnablement.Infrastructure.Services;
using AIEnablement.Utility.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Business.Orchestrators;

namespace AIEnablement.DocumentGeneration.App;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Load configuration
                var config = AppConfiguration.Load("appsettings.json");
                services.AddSingleton(config);

                // Register services
                services.AddSingleton<ILlmService, LlmService>();
                services.AddSingleton<IPromptService, PromptService>();
                services.AddSingleton<IToolIdeationService, ToolIdeationService>();
                services.AddSingleton<ITocGenerationService, TocGenerationService>();
                services.AddSingleton<ISectionGenerationService, SectionGenerationService>();
                services.AddSingleton<IValidationService, ValidationService>();
                services.AddSingleton<IDocumentAssemblyService, DocumentAssemblyService>();
                services.AddSingleton<DocumentGenerationOrchestrator>();

                // Logging
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        var orchestrator = host.Services.GetRequiredService<DocumentGenerationOrchestrator>();
        
        Console.WriteLine("=== Software Terms & Privacy Policy Generator ===");
        Console.WriteLine();

        await orchestrator.GenerateAllDocumentsAsync();

        Console.WriteLine();
        Console.WriteLine("Generation complete! Check the /data folder for output.");
    }
}
