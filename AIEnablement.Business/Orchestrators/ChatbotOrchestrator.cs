using AIEnablement.Core.Interfaces.Chatbot;
using AIEnablement.Core.Interfaces.Infrastructure;
using AIEnablement.Core.Models.Chatbot;
using Microsoft.Extensions.Logging;

namespace AIEnablement.Business.Orchestrators;

public class ChatbotOrchestrator
{
    private readonly IChatService _chatService;
    private readonly IConversationManager _conversationManager;
    private readonly IPromptService _promptService;
    private readonly ChatbotConfiguration _config;
    private readonly ILogger<ChatbotOrchestrator> _logger;

    public ChatbotOrchestrator(
        IChatService chatService,
        IConversationManager conversationManager,
        IPromptService promptService,
        ChatbotConfiguration config,
        ILogger<ChatbotOrchestrator> logger)
    {
        _chatService = chatService;
        _conversationManager = conversationManager;
        _promptService = promptService;
        _config = config;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        DisplayWelcomeBanner();
        InitializeConversation();
        await ConversationLoopAsync();
    }

    private void DisplayWelcomeBanner()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║  {_config.Assistant.Name,-54} Chatbot  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  /summary  - View conversation statistics");
        Console.WriteLine("  /history  - Show full conversation history");
        Console.WriteLine("  /clear    - Clear screen");
        Console.WriteLine("  /exit     - End conversation");
        Console.WriteLine();
    }

    private void InitializeConversation()
    {
        var systemPrompt = LoadSystemPrompt();
        _conversationManager.AddSystemMessage(systemPrompt);

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

            // Handle special commands
            if (userInput.StartsWith('/'))
            {
                if (!HandleCommand(userInput))
                    break;
                continue;
            }

            // Process user message
            _conversationManager.AddUserMessage(userInput);
            _conversationManager.TrimHistory();

            try
            {
                // Show thinking indicator
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"\n{_config.Assistant.Name} is thinking");
                var thinkingTask = AnimateThinking();

                // Get response
                var chatHistory = _conversationManager.GetChatHistory();
                var response = await _chatService.GetResponseAsync(chatHistory);

                // Stop animation
                await thinkingTask.DisposeAsync();
                Console.Write("\r" + new string(' ', 50) + "\r");
                Console.ResetColor();

                // Display response
                _conversationManager.AddAssistantMessage(response);

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

    private bool HandleCommand(string command)
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
                DisplaySummary();
                return true;

            case "/history":
                DisplayHistory();
                return true;

            case "/clear":
                Console.Clear();
                DisplayWelcomeBanner();
                return true;

            default:
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unknown command. Available: /summary, /history, /clear, /exit");
                Console.ResetColor();
                Console.WriteLine();
                return true;
        }
    }

    private void DisplaySummary()
    {
        var summary = _conversationManager.GetSummary();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n╔═══ Conversation Summary ═══╗");
        Console.WriteLine($"  Started: {summary.ConversationStarted:HH:mm:ss}");
        Console.WriteLine($"  Duration: {summary.Duration:mm\\:ss}");
        Console.WriteLine($"  Total Messages: {summary.TotalMessages}");
        Console.WriteLine($"    • Your Messages: {summary.UserMessages}");
        Console.WriteLine($"    • Assistant Messages: {summary.AssistantMessages}");
        Console.WriteLine($"    • GetCurrentDate Calls: {summary.GetCurrentDateToolCalls}");
        Console.WriteLine($"    • AddDaysToDate Calls: {summary.AddDaysToDateToolCalls}");
        Console.WriteLine("╚════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private void DisplayHistory()
    {
        var messages = _conversationManager.GetAllMessages();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n╔═══ Conversation History ═══╗");
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
            }
        }

        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╚════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    private string LoadSystemPrompt()
    {
        var data = _promptService.LoadPromptFromFile(_config.Assistant.PersonalityPromptPath);
        return data["system_prompt"];
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
}
