# AI Enablement Project

C# .NET 9 solution built in phases on Microsoft Semantic Kernel. The phases cover synthetic document generation, a conversational chatbot, semantic search over the generated documents, an agentic RAG assistant, and an evaluation framework for measuring RAG quality.

## Applications

| Phase | Application | Description |
|-------|-------------|-------------|
| 1 | `AIEnablement.DocumentGeneration.App` | Generates synthetic software terms and privacy policy documents (HTML) with injected data quality issues |
| 2 | `AIEnablement.Chatbot.App` | Console chatbot with personality, conversation history management and function calling (date tools) |
| 3 | `AIEnablement.SemanticSearch.App` | Ingests the generated HTML documents (parsing, chunking, embeddings) and provides interactive semantic search |
| 4 | `AIEnablement.AgenticRAGAssistant.App` | Chatbot that decides on its own when to search the documents via a retrieval tool and answers with sources |
| 5 | `AIEnablement.EvaluationQualityMetrics.App` | Runs experiments against a golden dataset and computes retrieval and answer quality metrics (LLM-as-a-judge) |

Shared libraries:

| Library | Responsibility |
|---------|----------------|
| `AIEnablement.Core` | Interfaces and models (configuration, DTOs) |
| `AIEnablement.Business` | Orchestrators and business services |
| `AIEnablement.Infrastructure` | LLM, embedding, vector store, prompt services and Semantic Kernel plugins |
| `AIEnablement.Utility` | Extensions and validation helpers |

## Project Structure

```text
AIEnablement.sln

AIEnablement.Core
├── Interfaces
│   ├── AgenticRAGAssistant
│   │   └── IAgenticRAGChatService.cs
│   ├── Chatbot
│   │   └── ILLMChatService.cs
│   ├── DocumentGeneration
│   │   ├── IDocumentAssemblyService.cs
│   │   ├── ILLMDocumentService.cs
│   │   ├── ISectionGenerationService.cs
│   │   ├── ITocGenerationService.cs
│   │   └── IToolIdeationService.cs
│   ├── EvaluationQualityMetrics
│   │   ├── IDatasetService.cs
│   │   ├── IExperimentRunner.cs
│   │   └── IMetricsService.cs
│   ├── Infrastructure
│   │   └── IPromptService.cs
│   └── SemanticSearch
│       ├── IChunkingService.cs
│       ├── IEmbeddingService.cs
│       ├── IHtmlParsingService.cs
│       ├── ISearchService.cs
│       └── IVectorStoreService.cs
└── Models
    ├── ConfigurationModels.cs
    ├── AgenticRAGAssistant
    │   └── RagChatbotConfiguration.cs
    ├── Chatbot
    │   ├── ChatbotConfiguration.cs
    │   └── ConversationModels.cs
    ├── DocumentGeneration
    │   └── Models.cs
    ├── EvaluationQualityMetrics
    │   ├── EvaluationConfiguration.cs
    │   └── EvaluationModels.cs
    └── SemanticSearch
        ├── SemanticSearchConfiguration.cs
        └── SemanticSearchModels.cs

AIEnablement.Business
├── Orchestrators
│   ├── ChatbotOrchestrator.cs
│   ├── DocumentGenerationOrchestrator.cs
│   ├── AgenticRAGAssistant
│   │   └── RagChatbotOrchestrator.cs
│   ├── EvaluationQualityMetrics
│   │   └── EvaluationOrchestrator.cs
│   └── SemanticSearch
│       ├── IngestionOrchestrator.cs
│       └── SearchOrchestrator.cs
└── Services
    ├── DocumentGeneration
    │   ├── DocumentAssemblyService.cs
    │   ├── SectionGenerationService.cs
    │   ├── TocGenerationService.cs
    │   └── ToolIdeationService.cs
    ├── EvaluationQualityMetrics
    │   └── ExperimentRunner.cs
    └── SemanticSearch
        ├── ChunkingService.cs
        └── SearchService.cs

AIEnablement.Infrastructure
├── Plugins
│   ├── DateToolsPlugin.cs
│   ├── RetrievalToolPlugin.cs
│   └── Filters
│       └── ManualParamsRetrievalFilter.cs
└── Services
    ├── LLMDocumentService.cs
    ├── LlmChatService.cs
    ├── PromptService.cs
    ├── AgenticRAGAssistant
    │   └── AgenticRAGChatService.cs
    ├── EvaluationQualityMetrics
    │   ├── DatasetService.cs
    │   └── MetricsService.cs
    └── SemanticSearch
        ├── EmbeddingService.cs
        ├── HtmlParsingService.cs
        └── VectorStoreService.cs

AIEnablement.Utility
├── Extensions
│   └── JsonExtensions.cs
└── Services
    └── ValidationService.cs

AIEnablement.DocumentGeneration.App          # Phase 1
├── appsettings.json
├── Program.cs
└── Prompts
    ├── section_generation.yaml
    ├── toc_generation.yaml
    └── tool_ideation.yaml

AIEnablement.Chatbot.App                     # Phase 2
├── appsettings.json
├── Program.cs
└── Prompts
    ├── chatbot_personality.yaml
    └── chatbot_summary_preserve.yaml

AIEnablement.SemanticSearch.App              # Phase 3
├── appsettings.json
└── Program.cs

AIEnablement.AgenticRAGAssistant.App         # Phase 4
├── appsettings.json
├── Program.cs
└── Prompts
    ├── chatbot_personality.yaml
    └── chatbot_summary_preserve.yaml

AIEnablement.EvaluationQualityMetrics.App    # Phase 5
├── appsettings.json
├── Program.cs
└── Prompts
    ├── answer_quality.yaml
    ├── chatbot_personality.yaml
    └── retrieval_metrics.yaml
```

## Setup

### Prerequisites

- .NET 9 SDK
- API key for an OpenAI-compatible endpoint (the default configuration targets a LiteLLM proxy)

### Installation

1. Clone/download the project
2. In the `appsettings.json` of each application you want to run, set the API key and endpoint:

```json
"ApiKeys": {
  "OpenAI": "sk-your-actual-api-key-here"
},
"ApiEndpoint": {
  "Endpoint": "https://your-openai-compatible-endpoint"
}
```

3. Restore packages:

```bash
dotnet restore
```

### Main Packages

- Microsoft.SemanticKernel
- Microsoft.SemanticKernel.Connectors.InMemory (vector store)
- Microsoft.ML.Tokenizers (token-based chunking, cl100k_base)
- Microsoft.Extensions.Hosting / DependencyInjection / Logging
- HtmlAgilityPack
- YamlDotNet

## Data Flow Between Applications

All paths in `appsettings.json` are relative to the application's working directory (e.g. `bin\Debug\net9.0\` when started from Visual Studio). Output of one phase must be copied to the working directory of the next one.

```text
Phase 1  DocumentGeneration   ──►  data/<tool>/*.html
                                        │
Phase 3  SemanticSearch       ingest ──►  vectorstore/vectorstore.json
                                        │
Phase 4  AgenticRAGAssistant  loads vectorstore ──►  /save golden ──►  goldendataset/golden_dataset.json
                                        │
Phase 5  EvaluationQualityMetrics  loads vectorstore + golden dataset ──►  results/<experiment>.json
```

---

## Phase 1 – Document Generation

Generates synthetic software terms and privacy policy documents.

### Features

- **Multi-Phase Generation**: Tool ideation -> ToC generation -> Section-by-section generation
- **Structured Outputs**: JSON schema enforcement using OpenAI's structured output feature
- **Data Quality Issues**: Automatic injection of contradictions, ambiguities, typos, and inconsistencies
- **Validation**: HTML and JSON validation for all generated artifacts
- **Configurable**: All parameters externalized in appsettings.json and YAML prompts

### Configuration

- **Generation.NumberOfTools**: Number of fictional tools to generate (default: 5)
- **Generation.DocumentsPerTool**: Documents per tool (default: 4)
- **Generation.AvailableDocumentTypes**: Document types to choose from
- **Models**: Separate model per phase:
  - `ToolIdeationModel`: Model for tool ideation (gpt-4o)
  - `TocGenerationModel`: Reasoning model for ToC (gpt-4o)
  - `SectionGenerationModel`: Cost-effective model (gpt-4o-mini)
- **Temperature & TopP**: Adjust for variety
- **DataQuality**: Min/max issues injected per document (default: 2-3)
- **Prompts**: Paths to `tool_ideation.yaml`, `toc_generation.yaml`, `section_generation.yaml`

### Running

```bash
dotnet run --project AIEnablement.DocumentGeneration.App
```

The application will:

1. Generate fictional software tools
2. For each tool, create documents (randomly selected types)
3. For each document:
   - Generate a structured ToC (JSON)
   - Generate sections iteratively with context of previous sections
   - Inject quality issues
   - Assemble into valid HTML
   - Validate HTML and JSON
4. Save outputs to the `data` folder (the folder is recreated on each run)

### Output Structure

```text
data/
├── tool1/
│   ├── toc_privacy_policy.json
│   ├── privacy_policy.html
│   ├── toc_terms_of_service.json
│   ├── terms_of_service.html
│   └── ...
├── tool2/
│   └── ...
└── issues.txt
```

### Quality Issues

- **ContradictoryClause**: A statement contradicting previous content or the same section
- **AmbiguousLanguage**: Unclear, undefined terms
- **MinorTypo**: Subtle typographical errors
- **InconsistentTerminology**: Inconsistent use of equivalent terms

---

## Phase 2 – Chatbot

Console chatbot ("HAL 9000") with a configurable personality.

### Features

- Personality defined in `Prompts/chatbot_personality.yaml`
- Conversation history limited to `MaxHistoryMessages`, older messages are summarized (`chatbot_summary_preserve.yaml`)
- Function calling via `DateToolsPlugin` (`get_current_date`, `add_days_to_date`)

### Running

```bash
dotnet run --project AIEnablement.Chatbot.App
```

Commands: `/summary`, `/history`, `/clear`, `/exit`

---

## Phase 3 – Data Ingestion & Semantic Search

### Features

- HTML parsing of Phase 1 documents (HtmlAgilityPack)
- Token-based chunking (`ChunkSize`, `ChunkOverlap`, cl100k_base tokenizer)
- Batched embeddings (`text-embedding-3-small`, 1536 dimensions)
- In-memory vector store persisted to `vectorstore/vectorstore.json`
- Search with `TopK`, `MinRelevanceScore` and optional neighbor chunks (`IncludeNeighbors`)

### Running

```bash
dotnet run --project AIEnablement.SemanticSearch.App
```

At the `Command:` prompt enter:

- `ingest` – index HTML documents from the `data` folder
- `search` – interactive semantic search (`/config` shows the current configuration, `/exit` exits)

---

## Phase 4 – Agentic RAG Assistant

Chatbot that combines Phase 2 and Phase 3: the model decides when to call the retrieval tool.

### Features

- `RetrievalToolPlugin.search_documents` with optional tool/product name filter and number of results
- `DateToolsPlugin` for date questions
- Token-budget based history management (`MaxInputTokens`, `SummarizationThreshold`, `KeepRecentCount`), summaries made with a lighter model (`ModelLightId`)
- Saving the conversation as a golden dataset for Phase 5

### Running

Requires `vectorstore/vectorstore.json` produced by Phase 3.

```bash
dotnet run --project AIEnablement.AgenticRAGAssistant.App
```

Commands: `/summary`, `/history`, `/clear`, `/save golden` (saves `goldendataset/golden_dataset.json`), `/exit`

---

## Phase 5 – Evaluation & Quality Metrics

Runs the RAG assistant with several configurations against the golden dataset and scores the results with an evaluator model.

### Metrics

| Group | Metrics |
|-------|---------|
| Retrieval | Contextual Precision, Contextual Recall, Context Utilization |
| Answer quality | Answer Relevancy, Faithfulness, Answer Correctness |

Each metric has a score and a reason. False positives / false negatives are tracked per experiment.

### Configuration

- **Experiments.Configurations**: list of experiments (model, Temperature, TopP, TopK, MinRelevanceScore, IncludeNeighbors). Defaults: `baseline`, `high_precision`, `cost_efficient`
- **Experiments.OutputDirectory**: output folder (default: `results`)
- **Metrics**: evaluator model and parameters
- **Dataset.GoldenDatasetPath**: default `goldendataset/golden_dataset.json`
- **EvaluationPromptPaths**: `retrieval_metrics.yaml`, `answer_quality.yaml`

### Running

Requires `vectorstore/vectorstore.json` (Phase 3) and the golden dataset (Phase 4).

```bash
dotnet run --project AIEnablement.EvaluationQualityMetrics.App
```

Commands:

- `load-dataset` – load the golden dataset
- `run-experiments` – run all configured experiments; results are saved as `results/<experiment>.json`
- `--help` – show help

---

## Architecture Decisions

### Why Semantic Kernel?

- Native .NET integration
- Strong typing with C# models
- Built-in structured output support and function calling
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

## Troubleshooting

- **API errors**: check the API key and endpoint in `appsettings.json`, rate limits and model availability
- **"No existing vector store found"**: run `ingest` in Phase 3 and copy the `vectorstore` folder to the working directory of Phase 4/5
- **"Dataset not found"**: save a golden dataset in Phase 4 (`/save golden`) and copy the `goldendataset` folder to Phase 5
- **Short documents (Phase 1)**: increase temperature or modify `section_generation.yaml`

## License

This is a POC project for evaluation purposes.
