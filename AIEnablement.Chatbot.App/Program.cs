using AIEnablement.Business.Orchestrators;
using AIEnablement.Core.Interfaces.Chatbot;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models.Chatbot;
using AIEnablement.Infrastructure.Plugins;
using AIEnablement.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Chatbot.App;

internal class Program
{
    static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                var config = ChatbotConfiguration.Load("appsettings.json");
                services.AddSingleton(config);

                services.AddSingleton<ILLMChatService, LLMChatService>();
                services.AddSingleton<IPromptService, PromptService>();
                services.AddSingleton<DateToolsPlugin>();
                services.AddSingleton<ChatbotOrchestrator>();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Warning);
                });
            })
            .Build();

        var orchestrator = host.Services.GetRequiredService<ChatbotOrchestrator>();
        await orchestrator.RunAsync();
    }
}