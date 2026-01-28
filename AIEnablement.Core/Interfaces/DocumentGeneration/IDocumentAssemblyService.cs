using AIEnablement.Core.Models.DocumentGeneration;

namespace AIEnablement.Core.Interfaces.DocumentGeneration;

public interface IDocumentAssemblyService
{
    string AssembleDocument(DocumentGenerationContext context, TocSectionNested tocSection,  bool shouldAddMetadata);
}
