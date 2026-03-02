
using AIEnablement.Business.Orchestrators.AgenticRAGAssistant;
using AIEnablement.Business.Services.SemanticSearch;
using AIEnablement.Core.Interfaces.AgenticRAGAssistant;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using AIEnablement.Core.Models.SemanticSearch;
using AIEnablement.Infrastructure.Plugins;
using AIEnablement.Infrastructure.Services;
using AIEnablement.Infrastructure.Services.AgenticRAGAssistant;
using AIEnablement.Infrastructure.Services.SemanticSearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var config = RagChatbotConfiguration.Load("appsettings.json");
                services.AddSingleton(config);

                var ragConfig = RagConfiguration.Load("appsettings.json");
                services.AddSingleton(ragConfig);

                // RAG services (from Phase 3)
                services.AddSingleton<IVectorStoreService, VectorStoreService>();
                services.AddSingleton<IEmbeddingService, EmbeddingService>();
                services.AddSingleton<ISearchService, SearchService>();
                services.AddSingleton<IPromptService, PromptService>();

                // Chatbot services
                services.AddSingleton<IAgenticRAGChatService, AgenticRAGChatService>();
                //services.AddSingleton<IConversationManager, ConversationManager>();
                //services.AddSingleton<IConversationSummaryService, ConversationSummaryService>();

                services.AddSingleton<DateToolsPlugin>();
                services.AddSingleton<RetrievalToolPlugin>();

                // Orchestrator
                services.AddSingleton<RagChatbotOrchestrator>();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .Build();

        var orchestrator = host.Services.GetRequiredService<RagChatbotOrchestrator>();
        await orchestrator.RunAsync();
    }
}
