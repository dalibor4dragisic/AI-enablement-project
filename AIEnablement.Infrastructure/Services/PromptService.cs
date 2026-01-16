using AIEnablement.Core.Interfaces.Infrastructure;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AIEnablement.Infrastructure.Services;
public class PromptService : IPromptService
{
    public Dictionary<string, object> LoadPromptFromFile(string path)
    {
        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Dictionary<string, object>>(yaml)
               ?? throw new InvalidOperationException($"Failed to load prompt from {path}");
    }
}
