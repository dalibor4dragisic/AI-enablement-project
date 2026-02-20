using AIEnablement.Core.Interfaces.SemanticSearch;
using AIEnablement.Core.Models.SemanticSearch;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using System.Collections.Generic;
using System.Text;
using static System.Collections.Specialized.BitVector32;

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

            // Use TikToken tokenizer for accurate token counting
            _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
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

            if (_config.Chunking.StructureAware)
            {
                chunks.AddRange(ChunkSectionStructureAware(section, metadata, ref chunkIndex));
            }
            else
            {
                chunks.AddRange(ChunkSectionSimple(section, metadata, ref chunkIndex));
            }

            // Process subsections recursively
            foreach (var subsection in section.Subsections)
            {
                chunks.AddRange(ChunkSection(subsection, metadata, ref chunkIndex));
            }

            return chunks;
        }

        //private List<TextChunk> ChunkSectionStructureAware1(ParsedSection section, DocumentMetadata metadata, ref int chunkIndex)
        //{
        //    var chunks = new List<TextChunk>();

        //    // Include heading if configured
        //    var contentWithHeading = _config.Chunking.PreserveHeadings
        //        ? $"{section.Heading}\n\n{section.Content}"
        //        : section.Content;

        //    var tokens = _tokenizer.EncodeToTokens(contentWithHeading, out _);
        //    var tokenCount = tokens.Count;

        //    // If section fits in one chunk, keep it together
        //    if (tokenCount <= _config.Chunking.ChunkSize)
        //    {
        //        chunks.Add(new TextChunk
        //        {
        //            ToolName = metadata.ToolName,
        //            DocumentType = metadata.DocumentType,
        //            FilePath = metadata.FilePath,
        //            SectionHeading = section.Heading,
        //            Content = section.Content,
        //            ChunkIndex = chunkIndex++,
        //            TokenCount = tokenCount
        //        });
        //        return chunks;
        //    }

        //    // Section is too large, split by sentences while respecting chunk size
        //    var sentences = SplitIntoSentences(section.Content);
        //    var currentChunkText = _config.Chunking.PreserveHeadings ? $"{section.Heading}\n\n" : "";
        //    var currentTokenCount = _config.Chunking.PreserveHeadings
        //        ? _tokenizer.EncodeToTokens(currentChunkText, out _).Count
        //        : 0;

        //    foreach (var sentence in sentences)
        //    {
        //        var sentenceTokens = _tokenizer.EncodeToTokens(sentence, out _);
        //        var sentenceTokenCount = sentenceTokens.Count;

        //        // If adding this sentence exceeds chunk size, save current chunk
        //        if (currentTokenCount + sentenceTokenCount > _config.Chunking.ChunkSize && currentTokenCount > 0)
        //        {
        //            if (currentChunkText.Length >= _config.Chunking.MinChunkSize)
        //            {
        //                chunks.Add(new TextChunk
        //                {
        //                    ToolName = metadata.ToolName,
        //                    DocumentType = metadata.DocumentType,
        //                    FilePath = metadata.FilePath,
        //                    SectionHeading = section.Heading,
        //                    Content = currentChunkText.Trim(),
        //                    ChunkIndex = chunkIndex++,
        //                    TokenCount = currentTokenCount
        //                });
        //            }

        //            // Start new chunk with overlap
        //            currentChunkText = GetOverlapText(sentences, sentences.IndexOf(sentence));
        //            currentTokenCount = _tokenizer.EncodeToTokens(currentChunkText, out _).Count;
        //        }

        //        currentChunkText += sentence + " ";
        //        currentTokenCount += sentenceTokenCount;
        //    }

        //    // Add remaining content
        //    if (currentChunkText.Length >= _config.Chunking.MinChunkSize)
        //    {
        //        chunks.Add(new TextChunk
        //        {
        //            ToolName = metadata.ToolName,
        //            DocumentType = metadata.DocumentType,
        //            FilePath = metadata.FilePath,
        //            SectionHeading = section.Heading,
        //            Content = currentChunkText.Trim(),
        //            ChunkIndex = chunkIndex++,
        //            TokenCount = currentTokenCount
        //        });
        //    }

        //    return chunks;
        //}

        private List<TextChunk> ChunkSectionStructureAware(ParsedSection section, DocumentMetadata metadata, ref int chunkIndex)
        {
            var chunks = new List<TextChunk>();

            var tokens = _tokenizer.EncodeToTokens(section.Content, out _);

            // If section fits in one chunk, keep it together
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
                    TokenCount = tokens.Count
                });
                return chunks;
            }

            // Section is too large, split by sentences while respecting chunk size
            var sentences = SplitIntoSentences(section.Content);

            var currentChunkText = string.Empty;
            var currentTokenCount = 0;

            foreach (var sentence in sentences)
            {
                var sentenceTokens = _tokenizer.EncodeToTokens(sentence, out _);
                var sentenceTokenCount = sentenceTokens.Count;

                // If adding this sentence exceeds chunk size, save current chunk
                if (currentTokenCount + sentenceTokenCount > _config.Chunking.ChunkSize && currentTokenCount > 0)
                {
                    if (currentChunkText.Length >= _config.Chunking.MinChunkSize)
                    {
                        chunks.Add(new TextChunk
                        {
                            ToolName = metadata.ToolName,
                            DocumentType = metadata.DocumentType,
                            FilePath = metadata.FilePath,
                            SectionHeading = section.Heading,
                            Content = currentChunkText.Trim(),
                            ChunkIndex = chunkIndex++,
                            TokenCount = currentTokenCount
                        });
                    }

                    // Start new chunk with overlap
                    currentChunkText = GetOverlapText(sentences, sentences.IndexOf(sentence));
                    currentTokenCount = _tokenizer.EncodeToTokens(currentChunkText, out _).Count;
                }

                currentChunkText += sentence + " ";
                currentTokenCount += sentenceTokenCount;
            }

            // Add remaining content
            if (currentChunkText.Length >= _config.Chunking.MinChunkSize)
            {
                chunks.Add(new TextChunk
                {
                    ToolName = metadata.ToolName,
                    DocumentType = metadata.DocumentType,
                    FilePath = metadata.FilePath,
                    SectionHeading = section.Heading,
                    Content = currentChunkText.Trim(),
                    ChunkIndex = chunkIndex++,
                    TokenCount = currentTokenCount
                });
            }

            return chunks;
        }

        private List<TextChunk> ChunkSectionSimple(ParsedSection section, DocumentMetadata metadata, ref int chunkIndex)
        {
            var chunks = new List<TextChunk>();
            var content = section.Content;
            var tokens = _tokenizer.EncodeToTokens(content, out _);

            for (int i = 0; i < tokens.Count; i += _config.Chunking.ChunkSize - _config.Chunking.ChunkOverlap)
            {
                var chunkTokens = tokens.Skip(i).Take(_config.Chunking.ChunkSize).ToList();
                var chunkText = _tokenizer.Decode(chunkTokens.Select(token => token.Id));

                if (chunkText.Length >= _config.Chunking.MinChunkSize)
                {
                    chunks.Add(new TextChunk
                    {
                        ToolName = metadata.ToolName,
                        DocumentType = metadata.DocumentType,
                        FilePath = metadata.FilePath,
                        SectionHeading = section.Heading,
                        Content = chunkText,
                        ChunkIndex = chunkIndex++,
                        TokenCount = chunkTokens.Count
                    });
                }
            }

            return chunks;
        }

        private List<string> SplitIntoSentences(string text)
        {
            var sentences = text.Split(new[] { ". ", "! ", "? ", ".\n", "!\n", "?\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.TrimEnd() + " ")
                .ToList();

            return sentences;
        }

        private string GetOverlapText(List<string> sentences, int currentIndex)
        {
            var overlapSentences = new List<string>();
            int overlapTokens = 0;

            // Go backwards to collect overlap
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
