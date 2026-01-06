using System.Diagnostics;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

/// <summary>
/// Caches and retrieves segment embeddings from the database.
/// </summary>
public sealed class EmbeddingCacheService : IEmbeddingCacheService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRunService;
    private readonly ILlmService _llmService;
    private const string EmbeddingModel = "nomic-embed-text";
    private const int EmbeddingDimensions = 768;

    public EmbeddingCacheService(
        ChatLakeDbContext db,
        IInferenceRunService inferenceRunService,
        ILlmService llmService)
    {
        _db = db;
        _inferenceRunService = inferenceRunService;
        _llmService = llmService;
    }

    public async Task<float[]?> GetOrGenerateAsync(
        long segmentId,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        var cached = await _db.SegmentEmbeddings
            .Where(e => e.ConversationSegmentId == segmentId && e.EmbeddingModel == EmbeddingModel)
            .FirstOrDefaultAsync(cancellationToken);

        if (cached != null)
        {
            // Verify content hash matches
            var segment = await _db.ConversationSegments
                .Where(s => s.ConversationSegmentId == segmentId)
                .Select(s => new { s.ContentHash })
                .FirstOrDefaultAsync(cancellationToken);

            if (segment != null && cached.SourceContentHash.SequenceEqual(segment.ContentHash))
            {
                return DeserializeEmbedding(cached.EmbeddingVector);
            }

            // Stale - remove and regenerate
            _db.SegmentEmbeddings.Remove(cached);
        }

        // Generate new embedding
        var segmentData = await _db.ConversationSegments
            .Where(s => s.ConversationSegmentId == segmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (segmentData == null)
            return null;

        var embedding = await _llmService.GenerateEmbeddingAsync(segmentData.ContentText, cancellationToken);
        if (embedding == null)
            return null;

        // Cache it (use existing inference run if available, or create minimal tracking)
        var embeddingEntity = new SegmentEmbedding
        {
            ConversationSegmentId = segmentId,
            InferenceRunId = segmentData.InferenceRunId, // Use segmentation run
            EmbeddingModel = EmbeddingModel,
            Dimensions = EmbeddingDimensions,
            EmbeddingVector = SerializeEmbedding(embedding),
            SourceContentHash = segmentData.ContentHash
        };

        _db.SegmentEmbeddings.Add(embeddingEntity);
        await _db.SaveChangesAsync(cancellationToken);

        return embedding;
    }

    public async Task<EmbeddingGenerationResult> GenerateMissingEmbeddingsAsync(
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Find segments without embeddings
        var existingEmbeddings = await _db.SegmentEmbeddings
            .Where(e => e.EmbeddingModel == EmbeddingModel)
            .Select(e => e.ConversationSegmentId)
            .ToHashSetAsync(cancellationToken);

        var segmentsToProcess = await _db.ConversationSegments
            .Where(s => !existingEmbeddings.Contains(s.ConversationSegmentId))
            .Select(s => new { s.ConversationSegmentId, s.ContentText, s.ContentHash, s.InferenceRunId })
            .ToListAsync(cancellationToken);

        if (segmentsToProcess.Count == 0)
        {
            Console.WriteLine("[EmbeddingCache] All segments have embeddings");
            return new EmbeddingGenerationResult(0, 0, 0, existingEmbeddings.Count, stopwatch.Elapsed);
        }

        Console.WriteLine($"[EmbeddingCache] Generating embeddings for {segmentsToProcess.Count} segments...");

        // Start inference run
        var runId = await _inferenceRunService.StartRunAsync(
            runType: "Embedding",
            modelName: EmbeddingModel,
            modelVersion: "1.0.0",
            inputScope: "Segments",
            featureConfigHash: new byte[32], // No config for embeddings
            inputDescription: $"Generating embeddings for {segmentsToProcess.Count} segments");

        try
        {
            var generated = 0;
            var lastReport = 0;

            foreach (var segment in segmentsToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Extract substantive content, skipping conversational openers
                var contentForEmbedding = ExtractSubstantiveContent(segment.ContentText);
                var embedding = await _llmService.GenerateEmbeddingAsync(contentForEmbedding, cancellationToken);
                if (embedding != null)
                {
                    var embeddingEntity = new SegmentEmbedding
                    {
                        ConversationSegmentId = segment.ConversationSegmentId,
                        InferenceRunId = runId,
                        EmbeddingModel = EmbeddingModel,
                        Dimensions = EmbeddingDimensions,
                        EmbeddingVector = SerializeEmbedding(embedding),
                        SourceContentHash = segment.ContentHash
                    };

                    _db.SegmentEmbeddings.Add(embeddingEntity);
                    generated++;
                }

                // Progress reporting
                var progress = (generated * 100) / segmentsToProcess.Count;
                if (progress >= lastReport + 10)
                {
                    Console.WriteLine($"[EmbeddingCache] Progress: {progress}% ({generated}/{segmentsToProcess.Count})");
                    lastReport = progress;

                    // Save periodically to avoid memory buildup
                    await _db.SaveChangesAsync(cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _inferenceRunService.CompleteRunAsync(runId,
                $"{{\"segmentsProcessed\":{segmentsToProcess.Count},\"embeddingsGenerated\":{generated}}}");

            stopwatch.Stop();
            Console.WriteLine($"[EmbeddingCache] Generated {generated} embeddings in {stopwatch.Elapsed.TotalSeconds:F1}s");

            return new EmbeddingGenerationResult(
                runId,
                segmentsToProcess.Count,
                generated,
                existingEmbeddings.Count,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRunService.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<SegmentEmbeddingData>> GetAllEmbeddingsAsync(
        CancellationToken cancellationToken = default)
    {
        var embeddings = await _db.SegmentEmbeddings
            .Where(e => e.EmbeddingModel == EmbeddingModel)
            .Join(_db.ConversationSegments,
                e => e.ConversationSegmentId,
                s => s.ConversationSegmentId,
                (e, s) => new { e.ConversationSegmentId, s.ConversationId, e.EmbeddingVector })
            .ToListAsync(cancellationToken);

        return embeddings
            .Select(e => new SegmentEmbeddingData(
                e.ConversationSegmentId,
                e.ConversationId,
                DeserializeEmbedding(e.EmbeddingVector)))
            .ToList();
    }

    public async Task<int> InvalidateStaleEmbeddingsAsync(
        CancellationToken cancellationToken = default)
    {
        // Find embeddings where the source content hash doesn't match
        var staleEmbeddings = await _db.SegmentEmbeddings
            .Join(_db.ConversationSegments,
                e => e.ConversationSegmentId,
                s => s.ConversationSegmentId,
                (e, s) => new { Embedding = e, Segment = s })
            .Where(x => x.Embedding.SourceContentHash != x.Segment.ContentHash)
            .Select(x => x.Embedding)
            .ToListAsync(cancellationToken);

        if (staleEmbeddings.Count > 0)
        {
            _db.SegmentEmbeddings.RemoveRange(staleEmbeddings);
            await _db.SaveChangesAsync(cancellationToken);
            Console.WriteLine($"[EmbeddingCache] Invalidated {staleEmbeddings.Count} stale embeddings");
        }

        return staleEmbeddings.Count;
    }

    private static byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var embedding = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
        return embedding;
    }

    /// <summary>
    /// Extracts substantive content from a segment, de-emphasizing conversational openers.
    /// Instead of filtering specific greetings, we skip short opening sentences and
    /// weight the middle/end of the content more heavily.
    /// </summary>
    private static string ExtractSubstantiveContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content;

        // Split into paragraphs (double newline) or sentences
        var paragraphs = content.Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);

        if (paragraphs.Length <= 1)
        {
            // Single paragraph - try sentence-level analysis
            return ExtractFromSingleParagraph(content);
        }

        // Multiple paragraphs: skip first if it's short (likely greeting/opener)
        var firstParagraph = paragraphs[0].Trim();
        if (firstParagraph.Length < 150 && paragraphs.Length > 1)
        {
            // First paragraph is short - likely "Hey Eva! Here's what I need..."
            // Use remaining paragraphs for embedding
            return string.Join("\n\n", paragraphs.Skip(1));
        }

        // First paragraph is substantial - use everything
        return content;
    }

    private static string ExtractFromSingleParagraph(string content)
    {
        // For single paragraphs, find sentence boundaries
        // Skip first sentence if it's very short (under 80 chars)
        var sentences = SplitIntoSentences(content);

        if (sentences.Count <= 1)
            return content;

        var firstSentence = sentences[0].Trim();
        if (firstSentence.Length < 80 && sentences.Count > 1)
        {
            // Short opener - skip it
            return string.Join(" ", sentences.Skip(1));
        }

        return content;
    }

    private static List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var current = new System.Text.StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            current.Append(text[i]);

            // Check for sentence-ending punctuation followed by space or end
            if ((text[i] == '.' || text[i] == '!' || text[i] == '?') &&
                (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1])))
            {
                var sentence = current.ToString().Trim();
                if (!string.IsNullOrEmpty(sentence))
                    sentences.Add(sentence);
                current.Clear();
            }
        }

        // Don't forget any remaining text
        var remaining = current.ToString().Trim();
        if (!string.IsNullOrEmpty(remaining))
            sentences.Add(remaining);

        return sentences;
    }
}
