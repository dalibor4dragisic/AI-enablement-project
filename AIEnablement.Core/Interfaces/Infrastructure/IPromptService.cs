namespace AIEnablement.Core.Interfaces.Infrastructure;

public interface IPromptService
{
    Dictionary<string, object> LoadPromptFromFile(string path);
}
