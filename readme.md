# Document Generator POC

C# .NET 9 console application for generating synthetic software terms and privacy policy documents using Semantic Kernel and LLM integration.

## Features

- **Dependency Injection**: Full DI container setup with Microsoft.Extensions.DependencyInjection
- **Semantic Kernel Integration**: LLM integration via Microsoft.SemanticKernel
- **Multi-Phase Generation**: Tool ideation → ToC generation → Section-by-section generation
- **Structured Outputs**: JSON schema enforcement using OpenAI's structured output feature
- **Data Quality Issues**: Automatic injection of contradictions, ambiguities, typos, and inconsistencies
- **Validation**: HTML and JSON validation for all generated artifacts
- **Configurable**: All parameters externalized in appsettings.json and YAML prompts

## Project Structure

```
DocumentGenerator/
├── Configuration/
│   └── AppConfiguration.cs          # Configuration models
├── Models/
│   └── DomainModels.cs              # Domain entities
├── Services/
│   ├── ILlmService.cs               # LLM abstraction
│   ├── SemanticKernelLlmService.cs  # SK implementation
│   ├── ToolIdeationService.cs       # Phase 1: Tool generation
│   ├── TocGenerationService.cs      # Phase 2: ToC generation
│   ├── SectionGenerationService.cs  # Phase 3: Section generation
│   ├── DocumentAssemblyService.cs   # HTML assembly
│   ├── ValidationService.cs         # Validation logic
│   └── DocumentGenerationOrchestrator.cs  # Main coordinator
├── prompts/
│   ├── tool_ideation.yaml
│   ├── toc_generation.yaml
│   └── section_generation.yaml
├── Program.cs                       # Entry point
├── appsettings.json                 # Configuration
└── DocumentGenerator.csproj
```

## Setup

### Prerequisites

- .NET 9 SDK
- OpenAI API key

### Installation

1. Clone/download the project
2. Update `appsettings.json` with your OpenAI API key:

```json
"ApiKeys": {
  "OpenAI": "sk-your-actual-api-key-here"
}
```

3. Restore packages:

```bash
dotnet restore
```

## Configuration

### appsettings.json

Configure all generation parameters:

- **NumberOfTools**: Number of fictional tools to generate (default: 5)
- **DocumentsPerTool**: Documents per tool (default: 4)
- **AvailableDocumentTypes**: Document types to choose from
- **Models**: Configure different models for each phase:
  - `TocGenerationModel`: Reasoning model for ToC (gpt-4o)
  - `SectionGenerationModel`: Cost-effective model (gpt-4o-mini)
  - `ToolIdeationModel`: Model for tool ideation (gpt-4o)
- **Temperature & TopP**: Adjust for variety
- **DataQuality**: Configure issue injection (2-3 issues per document)

### Prompt Files (prompts/*.yaml)

All prompts are externalized in YAML files:

- `tool_ideation.yaml`: Tool description generation
- `toc_generation.yaml`: Table of contents structure
- `section_generation.yaml`: Section content generation

Customize these to adjust output quality and style.

## Running

```bash
dotnet run
```

The application will:

1. Generate 5 fictional software tools
2. For each tool, create 4 documents (randomly selected types)
3. For each document:
   - Generate a structured ToC (JSON)
   - Generate sections iteratively with context
   - Inject 2-3 quality issues
   - Assemble into valid HTML
   - Validate HTML and JSON
4. Save outputs to `/data` folder

## Output Structure

```
data/
├── tool1_name/
│   ├── toc_privacy_policy.json
│   ├── privacy_policy.html
│   ├── toc_terms_of_service.json
│   ├── terms_of_service.html
│   └── ...
├── tool2_name/
│   └── ...
└── ...
```

## Key Implementation Details

### Multi-Model Strategy

- **ToC Generation**: Uses more capable model (gpt-4o) for reasoning and structure
- **Section Generation**: Uses cost-effective model (gpt-4o-mini) for content
- **Tool Ideation**: Configurable model (gpt-4o) for initial brainstorming

### Context Preservation

Each section generation receives:
- Tool description
- Document type
- All previously generated sections
- Enables coherent, flowing documents

### Structured Outputs

Uses OpenAI's structured output feature via Semantic Kernel:
```csharp
ResponseFormat = typeof(T)
```

Ensures valid JSON conforming to C# models.

### Quality Issues

Automatically injects issues based on configuration:
- **ContradictoryClause**: Conflicting statements
- **AmbiguousLanguage**: Vague terms without definitions
- **MinorTypo**: Subtle spelling errors
- **InconsistentTerminology**: Variable term usage

Tracked per section for analysis.

## Validation

- **HTML Validation**: HtmlAgilityPack ensures well-formed HTML
- **JSON Validation**: System.Text.Json validates ToC files
- Logs warnings but continues generation

## Customization

### Changing Number of Tools

```json
"NumberOfTools": 10
```

### Changing Models

```json
"Models": {
  "TocGenerationModel": {
    "ModelId": "gpt-4o",
    "Temperature": 0.5
  }
}
```

### Adjusting Output Length

Modify prompts in `prompts/section_generation.yaml`:
- Increase word count targets
- Add complexity requirements

### Adding Document Types

```json
"AvailableDocumentTypes": [
  "Privacy Policy",
  "Cookie Policy",
  "API Terms"
]
```

## Troubleshooting

### API Errors

- Check API key in appsettings.json
- Verify rate limits
- Check model availability

### Short Documents

- Increase temperature for more verbose output
- Modify section_generation.yaml prompts
- Ensure "previous_sections" context is included

### Missing Issues

- Check DataQuality configuration
- Verify issue injection logic in SectionGenerationService
- Review LLM responses in logs

## Architecture Decisions

### Why Semantic Kernel?

- Native .NET integration
- Strong typing with C# models
- Built-in structured output support
- Easy model switching

### Why Section-by-Section Generation?

- Maintains context across document
- Enables realistic document flow
- Allows progressive issue injection
- Achieves target document length (similar to real SLAs)

### Why YAML Prompts?

- Easy to edit without recompiling
- Version control friendly
- Supports multi-line prompts naturally
- Clear separation of code and prompts

## License

This is a POC project for evaluation purposes.
