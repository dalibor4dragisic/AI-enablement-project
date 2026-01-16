using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface ISectionGenerationService
{
    Task<GeneratedSection> GenerateSectionAsync(DocumentGenerationContext context, TocSection section);
}

