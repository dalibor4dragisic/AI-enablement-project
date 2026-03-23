using AIEnablement.Business.Services.SemanticSearch;
using AIEnablement.Core.Interfaces.AgenticRAGAssistant;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.AgenticRAGAssistant;
using AIEnablement.Core.Models.Chatbot;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIEnablement.Business.Orchestrators.AgenticRAGAssistant
{

    public class RagChatbotOrchestrator
    {
        private readonly IAgenticRAGChatService _chatService;
        private readonly IVectorStoreService _vectorStoreService;
        private readonly ISearchService _searchService;
        private readonly RagChatbotConfiguration _config;
        private readonly ILogger<RagChatbotOrchestrator> _logger;

        public RagChatbotOrchestrator(
            IAgenticRAGChatService chatService,
            IVectorStoreService vectorStoreService,
            ISearchService searchService,
            RagChatbotConfiguration config,
            ILogger<RagChatbotOrchestrator> logger)
        {
            _chatService = chatService;
            _vectorStoreService = vectorStoreService;
            _searchService = searchService;
            _config = config;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            DisplayWelcomeBanner();
            await InitializeAsync();
            await ConversationLoopAsync();
        }

        private void DisplayWelcomeBanner()
        {
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--------------------------------------------------------------");
            Console.WriteLine($"              Agentic RAG Assistant - Phase 4");
            Console.WriteLine("--------------------------------------------------------------");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("I can help you search and understand software documentation,");
            Console.WriteLine("privacy policies, terms of service, and legal documents.");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  /summary  - View conversation statistics");
            Console.WriteLine("  /history  - Show full conversation history");
            Console.WriteLine("  /clear    - Clear screen");
            Console.WriteLine("  /exit     - End conversation");
            Console.WriteLine("  /save golden     - Save golden dataset");
            Console.WriteLine();
        }

        private async Task InitializeAsync()
        {
            Console.WriteLine("Loading vector store...");

            try
            {
                await _vectorStoreService.LoadFromDiskAsync();

                //Load chunk metadata for neighbor expansion
                LoadChunkMetadataAsync();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Vector store loaded successfully");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Failed to load vector store: {ex.Message}");
                Console.WriteLine("Make sure Phase 3 ingestion has been completed.");
                Console.ResetColor();
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{_config.Assistant.Name}: Hello! How can I help you today?");
            Console.ResetColor();
            Console.WriteLine();
        }

        private async Task ConversationLoopAsync()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("You: ");
                Console.ResetColor();

                var userInput = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(userInput))
                    continue;

                //Handle commands
                if (userInput.StartsWith('/'))
                {
                    if (!await HandleCommandAsync(userInput))
                        break;
                    continue;
                }

                try
                {
                    //Show thinking indicator
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\n{_config.Assistant.Name} is thinking");
                    var thinkingTask = AnimateThinking();

                    //Get response
                    var response = await _chatService.GetResponseAsync(userInput);

                    //Stop animation
                    thinkingTask.Dispose();
                    Console.Write("\r" + new string(' ', 50) + "\r");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"{_config.Assistant.Name}: {response}");
                    Console.ResetColor();

                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
        }

        private async Task<bool> HandleCommandAsync(string command)
        {
            switch (command.ToLower())
            {
                case "/exit":
                case "/quit":
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n{_config.Assistant.Name}: Goodbye! Have a great day!");
                    Console.ResetColor();
                    return false;

                case "/summary":
                    await DisplaySummaryAsync();
                    return true;

                case "/history":
                    DisplayHistory();
                    return true;

                case "/clear":
                    Console.Clear();
                    DisplayWelcomeBanner();
                    return true;

                case "/save golden":
                    Console.Clear();
                    await SaveGoldenDataSetToDiskAsync();
                    return true;

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Unknown command. Available: /summary, /history, /clear, /exit");
                    Console.ResetColor();
                    Console.WriteLine();
                    return true;
            }
        }

        private async Task DisplaySummaryAsync()
        {
            var stats = await _chatService.GetSummary();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Conversation Statistics ---");
            Console.WriteLine($"  Started: {stats.ConversationStarted:HH:mm:ss}");
            Console.WriteLine($"  Duration: {stats.Duration:mm\\:ss}");
            Console.WriteLine($"  Total Messages: {stats.TotalMessages}");
            Console.WriteLine($"    • Your Messages: {stats.UserMessages}");
            Console.WriteLine($"    • Assistant Messages: {stats.AssistantMessages}");
            Console.WriteLine($"    • Tool GetCurrentDate Calls: {stats.GetCurrentDateToolCalls}");
            Console.WriteLine($"    • Tool AddDaysToDate Calls: {stats.AddDaysToDateToolCalls}");
            Console.WriteLine($"    • Tool SearchDocuments Calls: {stats.SearchDocumentsToolCalls}");
            Console.WriteLine($"  Estimated Tokens: {stats.EstimatedTokens:N0}");
            Console.WriteLine($"  Token Limit: {_config.Model.MaxInputTokens:N0}");

            var percentage = (double)stats.EstimatedTokens / _config.Model.MaxInputTokens * 100;
            Console.WriteLine($"  Usage: {percentage:F1}%");

            if (percentage > _config.Assistant.SummarizationThreshold * 100)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Approaching token limit - summarization will trigger soon");
            }

            Console.WriteLine($"{stats.SummaryText}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void DisplayHistory()
        {
            var messages = _chatService.GetAllMessages();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Conversation History ---");
            Console.ResetColor();

            foreach (var msg in messages.Where(m => m.Role != ChatRole.System))
            {
                var timestamp = msg.Timestamp.ToString("HH:mm:ss");

                switch (msg.Role)
                {
                    case ChatRole.User:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"[{timestamp}] You: {msg.Content}");
                        break;
                    case ChatRole.Assistant:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{timestamp}] {_config.Assistant.Name}: {msg.Content}");
                        break;
                    case ChatRole.Tool:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"[{timestamp}] Tool: {msg.Content}");
                        break;
                }
            }

            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("-------------------------------");
            Console.ResetColor();
            Console.WriteLine();
        }

        private Timer AnimateThinking()
        {
            int dots = 0;
            return new Timer(_ =>
            {
                dots = (dots + 1) % 4;
                Console.Write("\r" + new string(' ', 50));
                Console.Write($"\r{_config.Assistant.Name} is thinking" + new string('.', dots));
            }, null, 0, 500);
        }

        private void LoadChunkMetadataAsync()
        {
            var chunks = _vectorStoreService.GetChunks();

            _searchService.IndexChunksForNeighborLookup(chunks);
        }

        private void GenerateGoldenDataset()
        {
            
        }

        private async Task SaveGoldenDataSetToDiskAsync()
        {
            var goldenDataset = _chatService.CreateGoldenDatasetFromHistory();

            if (goldenDataset == null || !goldenDataset.Items.Any())
            {
                return;
            }

            var directory = "goldendataset";
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, "golden_dataset.json");

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            await using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, goldenDataset, options);

            _logger.LogInformation("Golden dataset saved to {Path} ({Count} items)",
                filePath,
                goldenDataset.Items.Count);
        }
    }
}
