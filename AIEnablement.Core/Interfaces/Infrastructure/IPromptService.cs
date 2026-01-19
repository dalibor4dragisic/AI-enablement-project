namespace AIEnablement.Core.Interfaces.Infrastructure;

public interface IPromptService
{
    Dictionary<string, string> LoadPromptFromFile(string path);
}
