using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface ITocGenerationService
{
    Task<TableOfContents> GenerateTocAsync(ToolDescription tool);
}
