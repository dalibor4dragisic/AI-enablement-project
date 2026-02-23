using System.Text.RegularExpressions;
using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;

namespace AIEnablement.Business.Services.SemanticSearch
{
    public class ChunkingService : IChunkingService
    {
        private readonly RagConfiguration _config;
        private readonly ILogger<ChunkingService> _logger;
        private readonly Tokenizer _tokenizer;

        public ChunkingService(
            RagConfiguration config,
            ILogger<ChunkingService> logger)
        {
            _config = config;
            _logger = logger;

            _tokenizer = TiktokenTokenizer.CreateForModel(config.Chunking.TokenizerModel);
        }

        public List<TextChunk> ChunkDocument(ParsedDocument document)
        {
            var chunks = new List<TextChunk>();
            int chunkIndex = 0;

            foreach (var section in document.Sections)
            {
                var sectionChunks = ChunkSection(section, document.Metadata, ref chunkIndex);
                chunks.AddRange(sectionChunks);
            }

            _logger.LogInformation("Created {Count} chunks for {Tool}/{DocType}",
                chunks.Count, document.Metadata.ToolName, document.Metadata.DocumentType);

            return chunks;
        }

        private List<TextChunk> ChunkSection(ParsedSection section, DocumentMetadata metadata, ref int chunkIndex)
        {
            var chunks = new List<TextChunk>();

            chunks.AddRange(ChunkSectionStructureAware(section, metadata, ref chunkIndex));

            return chunks;
        }

        private List<TextChunk> ChunkSectionStructureAware(ParsedSection section, DocumentMetadata metadata, ref int chunkIndex)
        {
            var chunks = new List<TextChunk>();

            var tokens = _tokenizer.EncodeToTokens(section.Content, out _);

            //If section fits in one chunk, keep it together
            if (tokens.Count <= _config.Chunking.ChunkSize)
            {
                chunks.Add(new TextChunk
                {
                    ToolName = metadata.ToolName,
                    DocumentType = metadata.DocumentType,
                    FilePath = metadata.FilePath,
                    SectionHeading = section.Heading,
                    Content = section.Content,
                    ChunkIndex = chunkIndex++,
                    TokenCount = tokens.Count,
                    SectionId = section.SectionId
                });
                return chunks;
            }

            //Section is too large, split by sentences while respecting chunk size
            var sentences = SplitIntoSentences(section.Content);

            var currentChunkText = string.Empty;
            var currentTokenCount = 0;

            foreach (var sentence in sentences)
            {
                var sentenceTokens = _tokenizer.EncodeToTokens(sentence, out _);
                var sentenceTokenCount = sentenceTokens.Count;

                //If adding this sentence exceeds chunk size, save current chunk
                if (currentTokenCount + sentenceTokenCount > _config.Chunking.ChunkSize && currentTokenCount > 0)
                {
                    chunks.Add(new TextChunk
                    {
                        ToolName = metadata.ToolName,
                        DocumentType = metadata.DocumentType,
                        FilePath = metadata.FilePath,
                        SectionHeading = section.Heading,
                        Content = currentChunkText.Trim(),
                        ChunkIndex = chunkIndex++,
                        TokenCount = currentTokenCount,
                        SectionId = section.SectionId
                    });

                    //Start new chunk with overlap
                    currentChunkText = GetOverlapText(sentences, sentences.IndexOf(sentence));
                    currentTokenCount = _tokenizer.EncodeToTokens(currentChunkText, out _).Count;
                }

                currentChunkText += sentence + " ";
                currentTokenCount += sentenceTokenCount;
            }

            //Add remaining content
            if (currentChunkText.Length > 0)
            {
                chunks.Add(new TextChunk
                {
                    ToolName = metadata.ToolName,
                    DocumentType = metadata.DocumentType,
                    FilePath = metadata.FilePath,
                    SectionHeading = section.Heading,
                    Content = currentChunkText.Trim(),
                    ChunkIndex = chunkIndex++,
                    TokenCount = currentTokenCount,
                    SectionId = section.SectionId
                });
            }

            return chunks;
        }

        private List<string> SplitIntoSentences(string text)
        {
            //var sentences = text.Split(new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries)
            //    .Select(s => s.TrimEnd() + " ")
            //    .ToList();

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                //.Select(s => s.Trim() + " ")
                .ToList();

            return sentences;
        }

        private string GetOverlapText(List<string> sentences, int currentIndex)
        {
            var overlapSentences = new List<string>();
            var overlapTokens = 0;

            //Go backwards to collect overlap
            for (int i = currentIndex - 1; i >= 0 && overlapTokens < _config.Chunking.ChunkOverlap; i--)
            {
                var sentenceTokens = _tokenizer.EncodeToTokens(sentences[i], out _).Count;
                if (overlapTokens + sentenceTokens <= _config.Chunking.ChunkOverlap)
                {
                    overlapSentences.Insert(0, sentences[i]);
                    overlapTokens += sentenceTokens;
                }
                else
                {
                    break;
                }
            }

            return string.Join(" ", overlapSentences) + " ";
        }
    }
}
