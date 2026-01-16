using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface IToolIdeationService
{
    Task<List<ToolDescription>> GenerateToolDescriptionsAsync();
}
