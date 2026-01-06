using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Conversations.Entities;
using ChatLake.Infrastructure.Logging;
using ChatLake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Conversations.Services;

/// <summary>
/// Segments conversations into topic-coherent units using embedding similarity.
/// </summary>
public sealed class SegmentationService : ISegmentationService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRunService;
    private readonly ILlmService _llmService;

    public SegmentationService(
        ChatLakeDbContext db,
        IInferenceRunService inferenceRunService,
        ILlmService llmService)
    {
        _db = db;
        _inferenceRunService = inferenceRunService;
        _llmService = llmService;
    }

    public async Task<SegmentationResult> SegmentConversationsAsync(
        SegmentationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SegmentationOptions();
        var stopwatch = Stopwatch.StartNew();

        // Find conversations that haven't been segmented yet
        var segmentedConversationIds = await _db.ConversationSegments
            .Select(s => s.ConversationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var conversationIds = await _db.Conversations
            .Where(c => !segmentedConversationIds.Contains(c.ConversationId))
            .Select(c => c.ConversationId)
            .ToListAsync(cancellationToken);

        if (conversationIds.Count == 0)
        {
            ConsoleLog.Info("Segmentation", "No new conversations to segment");
            return new SegmentationResult(0, 0, 0, stopwatch.Elapsed);
        }

        ConsoleLog.Info("Segmentation", $"Processing {conversationIds.Count} conversations...");

        // Start inference run
        var runId = await _inferenceRunService.StartRunAsync(
            runType: "Segmentation",
            modelName: "nomic-embed-text",
            modelVersion: "1.0.0",
            inputScope: "All",
            featureConfigHash: ComputeConfigHash(options),
            inputDescription: $"Segmenting {conversationIds.Count} conversations");

        try
        {
            var totalSegments = 0;
            var processed = 0;

            foreach (var conversationId in conversationIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var segments = await SegmentConversationInternalAsync(
                    conversationId, runId, options, cancellationToken);

                totalSegments += segments.Count;
                processed++;

                // Show progress every 50 conversations, or for long-running individual conversations
                if (processed % 50 == 0 || processed == conversationIds.Count)
                {
                    ConsoleLog.Progress("Segmentation", processed, conversationIds.Count, $"{totalSegments} segments created");
                }
            }

            await _inferenceRunService.CompleteRunAsync(runId,
                $"{{\"conversationsProcessed\":{processed},\"segmentsCreated\":{totalSegments}}}");

            stopwatch.Stop();
            return new SegmentationResult(runId, processed, totalSegments, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRunService.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    private async Task<List<ConversationSegment>> SegmentConversationInternalAsync(
        long conversationId,
        long runId,
        SegmentationOptions options,
        CancellationToken cancellationToken)
    {
        // Load messages for this conversation
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SequenceIndex)
            .Select(m => new { m.SequenceIndex, m.Role, m.Content })
            .ToListAsync(cancellationToken);

        // Filter out system messages and profile context
        var filteredMessages = messages
            .Where(m => m.Role != "system")
            .Where(m => !IsProfileContextMessage(m.Content))
            .ToList();

        // Skip conversations with too few substantive messages (likely profile-only or trivial)
        // Minimum of 3 messages needed for meaningful segmentation
        if (filteredMessages.Count < options.MinConversationMessages)
        {
            return [];
        }

        // Also skip if total content is too short (quick Q&A or greetings)
        var totalContentLength = filteredMessages.Sum(m => m.Content?.Length ?? 0);
        if (totalContentLength < options.MinContentLength)
        {
            return [];
        }

        // Find segment boundaries using embedding similarity
        var boundaries = await DetectTopicBoundariesAsync(
            filteredMessages.Select(m => m.Content).ToList(),
            options,
            cancellationToken);

        // Create segments based on boundaries
        var segments = new List<ConversationSegment>();
        var segmentIndex = 0;

        for (int i = 0; i < boundaries.Count; i++)
        {
            var startIdx = boundaries[i];
            var endIdx = (i + 1 < boundaries.Count) ? boundaries[i + 1] - 1 : filteredMessages.Count - 1;

            // Map back to original message indices
            var startMessageIndex = filteredMessages[startIdx].SequenceIndex;
            var endMessageIndex = filteredMessages[endIdx].SequenceIndex;

            // Concatenate segment content
            var segmentContent = string.Join("\n\n",
                filteredMessages.Skip(startIdx).Take(endIdx - startIdx + 1).Select(m => m.Content));

            var segment = new ConversationSegment
            {
                ConversationId = conversationId,
                SegmentIndex = segmentIndex++,
                StartMessageIndex = startMessageIndex,
                EndMessageIndex = endMessageIndex,
                MessageCount = endIdx - startIdx + 1,
                ContentText = segmentContent,
                ContentHash = SHA256.HashData(Encoding.UTF8.GetBytes(segmentContent)),
                InferenceRunId = runId
            };

            _db.ConversationSegments.Add(segment);
            segments.Add(segment);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return segments;
    }

    private async Task<List<int>> DetectTopicBoundariesAsync(
        List<string> messageContents,
        SegmentationOptions options,
        CancellationToken cancellationToken)
    {
        var boundaries = new List<int> { 0 }; // Always start at 0

        if (messageContents.Count <= options.MinSegmentSize)
        {
            // Too short to segment - return as single segment
            return boundaries;
        }

        // Create sliding windows and generate embeddings
        var windowEmbeddings = new List<float[]?>();
        var windowSize = Math.Min(options.WindowSize, messageContents.Count);

        for (int i = 0; i <= messageContents.Count - windowSize; i++)
        {
            var windowText = string.Join("\n", messageContents.Skip(i).Take(windowSize));

            // Truncate to avoid Ollama context length errors (8192 tokens ≈ 32K chars, use 24K to be safe)
            if (windowText.Length > 24000)
                windowText = windowText[..24000];

            var embedding = await _llmService.GenerateEmbeddingAsync(windowText, cancellationToken);
            windowEmbeddings.Add(embedding);
        }

        // Compare consecutive windows
        var lastBoundary = 0;
        for (int i = 1; i < windowEmbeddings.Count; i++)
        {
            var prev = windowEmbeddings[i - 1];
            var curr = windowEmbeddings[i];

            if (prev == null || curr == null)
                continue;

            var similarity = CosineSimilarity(prev, curr);

            // Check if we should create a boundary
            var messagesSinceLastBoundary = i - lastBoundary;

            if (similarity < options.SimilarityThreshold && messagesSinceLastBoundary >= options.MinSegmentSize)
            {
                // Topic shift detected - mark boundary
                boundaries.Add(i);
                lastBoundary = i;
            }
            else if (messagesSinceLastBoundary >= options.MaxSegmentSize)
            {
                // Force split at max size
                boundaries.Add(i);
                lastBoundary = i;
            }
        }

        return boundaries;
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator == 0 ? 0 : (float)(dotProduct / denominator);
    }

    private static bool IsProfileContextMessage(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("{"))
            return false;

        return trimmed.Contains("\"content_type\"") &&
               (trimmed.Contains("\"user_editable_context\"") ||
                trimmed.Contains("\"user_profile\"") ||
                trimmed.Contains("\"user_instructions\""));
    }

    public async Task<IReadOnlyList<SegmentInfo>> GetSegmentsAsync(long conversationId)
    {
        var segments = await _db.ConversationSegments
            .Where(s => s.ConversationId == conversationId)
            .OrderBy(s => s.SegmentIndex)
            .Select(s => new SegmentInfo(
                s.ConversationSegmentId,
                s.ConversationId,
                s.SegmentIndex,
                s.StartMessageIndex,
                s.EndMessageIndex,
                s.MessageCount,
                s.ContentText.Length > 100 ? s.ContentText.Substring(0, 100) + "..." : s.ContentText))
            .ToListAsync();

        return segments;
    }

    public async Task<IReadOnlyList<SegmentInfo>> GetSegmentsWithoutEmbeddingsAsync(
        string embeddingModel = "nomic-embed-text")
    {
        var segmentsWithEmbeddings = await _db.SegmentEmbeddings
            .Where(e => e.EmbeddingModel == embeddingModel)
            .Select(e => e.ConversationSegmentId)
            .ToListAsync();

        var segments = await _db.ConversationSegments
            .Where(s => !segmentsWithEmbeddings.Contains(s.ConversationSegmentId))
            .Select(s => new SegmentInfo(
                s.ConversationSegmentId,
                s.ConversationId,
                s.SegmentIndex,
                s.StartMessageIndex,
                s.EndMessageIndex,
                s.MessageCount,
                s.ContentText.Length > 100 ? s.ContentText.Substring(0, 100) + "..." : s.ContentText))
            .ToListAsync();

        return segments;
    }

    private static byte[] ComputeConfigHash(SegmentationOptions options)
    {
        var config = $"{options.WindowSize}|{options.SimilarityThreshold}|{options.MinSegmentSize}|{options.MaxSegmentSize}|{options.MinConversationMessages}|{options.MinContentLength}";
        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }

    public async Task<int> ResetAllSegmentsAsync(CancellationToken cancellationToken = default)
    {
        // Delete embeddings first (foreign key constraint)
        var embeddingsDeleted = await _db.SegmentEmbeddings.ExecuteDeleteAsync(cancellationToken);
        ConsoleLog.Info("Segmentation", $"Deleted {embeddingsDeleted} segment embeddings");

        // Delete all segments
        var segmentsDeleted = await _db.ConversationSegments.ExecuteDeleteAsync(cancellationToken);
        ConsoleLog.Info("Segmentation", $"Deleted {segmentsDeleted} segments");

        return segmentsDeleted;
    }
}
