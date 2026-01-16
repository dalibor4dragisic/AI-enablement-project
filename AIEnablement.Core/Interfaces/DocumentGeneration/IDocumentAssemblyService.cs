using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface IDocumentAssemblyService
{
    string AssembleDocument(DocumentGenerationContext context, TocSection tocSection,  bool shouldAddMetadata);
}
