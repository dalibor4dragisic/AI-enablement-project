using AIEnablement.Core.Models;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface ILLMDocumentService
{
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, ModelSettings modelSettings);
    Task<T> GenerateStructuredOutputAsync<T>(string systemPrompt, string userPrompt, ModelSettings modelSettings) where T : class;
}
