using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Gold.Entities;
using ChatLake.Infrastructure.Persistence;
using ChatLake.Inference.Topics;
using Microsoft.EntityFrameworkCore;

namespace ChatLake.Infrastructure.Gold.Services;

/// <summary>
/// Service for extracting topics from conversations using LDA.
/// </summary>
public sealed class TopicExtractionService : ITopicExtractionService
{
    private readonly ChatLakeDbContext _db;
    private readonly IInferenceRunService _inferenceRuns;

    public TopicExtractionService(ChatLakeDbContext db, IInferenceRunService inferenceRuns)
    {
        _db = db;
        _inferenceRuns = inferenceRuns;
    }

    private const string ModelName = "ChatLake.LDA";

    public async Task<TopicExtractionResult> ExtractTopicsAsync(TopicExtractionOptions? options = null)
    {
        options ??= new TopicExtractionOptions();
        var stopwatch = Stopwatch.StartNew();

        // Load all conversations with their message text
        var conversations = await LoadConversationsAsync();

        if (conversations.Count == 0)
        {
            return new TopicExtractionResult(
                InferenceRunId: 0,
                ConversationCount: 0,
                TopicCount: 0,
                AssignmentsCreated: 0,
                Duration: stopwatch.Elapsed);
        }

        // Compute feature config hash for reproducibility
        var configHash = ComputeConfigHash(options);

        // Start inference run
        var runId = await _inferenceRuns.StartRunAsync(
            runType: "Topics",
            modelName: ModelName,
            modelVersion: options.ModelVersion,
            inputScope: "All",
            featureConfigHash: configHash,
            inputDescription: $"Extracting {options.TopicCount} topics from {conversations.Count} conversations");

        try
        {
            // Run topic extraction pipeline
            var pipeline = new TopicExtractionPipeline(seed: 42);
            var result = pipeline.ExtractTopics(
                conversations,
                topicCount: options.TopicCount,
                keywordsPerTopic: options.KeywordsPerTopic);

            // Compute keywords from high-scoring documents for each topic
            var topicsWithKeywords = await ComputeTopicKeywordsAsync(
                result.Topics,
                result.Assignments,
                conversations,
                options.KeywordsPerTopic);

            // Save topics to database
            var topicIdMap = await SaveTopicsAsync(runId, topicsWithKeywords);

            // Save topic assignments above threshold
            var assignmentsCreated = await SaveAssignmentsAsync(
                runId,
                result.Assignments,
                topicIdMap,
                options.MinScoreThreshold);

            var metrics = JsonSerializer.Serialize(new
            {
                conversationCount = conversations.Count,
                topicCount = options.TopicCount,
                assignmentsCreated
            });

            await _inferenceRuns.CompleteRunAsync(runId, metrics);

            stopwatch.Stop();

            return new TopicExtractionResult(
                InferenceRunId: runId,
                ConversationCount: conversations.Count,
                TopicCount: options.TopicCount,
                AssignmentsCreated: assignmentsCreated,
                Duration: stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            await _inferenceRuns.FailRunAsync(runId, ex.Message);
            throw;
        }
    }

    private static byte[] ComputeConfigHash(TopicExtractionOptions options)
    {
        var config = JsonSerializer.Serialize(new
        {
            modelName = ModelName,
            modelVersion = options.ModelVersion,
            topicCount = options.TopicCount,
            keywordsPerTopic = options.KeywordsPerTopic,
            minScoreThreshold = options.MinScoreThreshold,
            seed = 42
        });

        return SHA256.HashData(Encoding.UTF8.GetBytes(config));
    }

    public async Task<IReadOnlyList<TopicDto>> GetTopicsAsync(long inferenceRunId)
    {
        var topics = await _db.Topics
            .Where(t => t.InferenceRunId == inferenceRunId)
            .ToListAsync();

        var topicIds = topics.Select(t => t.TopicId).ToList();

        var conversationCounts = await _db.ConversationTopics
            .Where(ct => topicIds.Contains(ct.TopicId))
            .GroupBy(ct => ct.TopicId)
            .Select(g => new { TopicId = g.Key, Count = g.Select(x => x.ConversationId).Distinct().Count() })
            .ToDictionaryAsync(x => x.TopicId, x => x.Count);

        return topics.Select(t => new TopicDto(
            TopicId: t.TopicId,
            Label: t.Label,
            Keywords: ParseKeywords(t.KeywordsJson),
            ConversationCount: conversationCounts.GetValueOrDefault(t.TopicId, 0)
        )).ToList();
    }

    public async Task<IReadOnlyList<ConversationTopicDto>> GetConversationTopicsAsync(long conversationId)
    {
        return await _db.ConversationTopics
            .Where(ct => ct.ConversationId == conversationId)
            .OrderByDescending(ct => ct.Score)
            .Select(ct => new ConversationTopicDto(
                TopicId: ct.TopicId,
                TopicLabel: ct.Topic.Label,
                Score: ct.Score))
            .ToListAsync();
    }

    private async Task<List<ConversationTextData>> LoadConversationsAsync()
    {
        // Get conversations with their message text concatenated
        var conversations = await _db.Conversations
            .Select(c => new
            {
                c.ConversationId,
                Messages = _db.Messages
                    .Where(m => m.ConversationId == c.ConversationId)
                    .OrderBy(m => m.SequenceIndex)
                    .Select(m => m.Content)
                    .ToList()
            })
            .ToListAsync();

        return conversations
            .Where(c => c.Messages.Any())
            .Select(c => new ConversationTextData
            {
                ConversationId = c.ConversationId,
                Text = string.Join(" ", c.Messages)
            })
            .ToList();
    }

    private Task<List<ExtractedTopic>> ComputeTopicKeywordsAsync(
        IReadOnlyList<ExtractedTopic> topics,
        IReadOnlyList<TopicAssignment> assignments,
        List<ConversationTextData> conversations,
        int keywordsPerTopic)
    {
        // For each topic, find top-scoring conversations and extract common words
        var conversationTexts = conversations.ToDictionary(c => c.ConversationId, c => c.Text);
        var result = new List<ExtractedTopic>();

        foreach (var topic in topics)
        {
            // Get top conversations for this topic
            var topConversations = assignments
                .Where(a => a.TopicIndex == topic.TopicIndex && a.Score > 0.1m)
                .OrderByDescending(a => a.Score)
                .Take(20)
                .ToList();

            // Extract keywords from these conversations
            var keywords = ExtractKeywordsFromConversations(
                topConversations.Select(a => a.ConversationId),
                conversationTexts,
                keywordsPerTopic);

            // Generate a better label from top keywords
            var label = keywords.Any()
                ? string.Join(", ", keywords.Take(3))
                : topic.Label;

            result.Add(new ExtractedTopic(
                TopicIndex: topic.TopicIndex,
                Label: label,
                Keywords: keywords));
        }

        return Task.FromResult(result);
    }

    private static List<string> ExtractKeywordsFromConversations(
        IEnumerable<long> conversationIds,
        Dictionary<long, string> conversationTexts,
        int count)
    {
        // Simple TF approach: count word frequencies across conversations
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in conversationIds)
        {
            if (!conversationTexts.TryGetValue(id, out var text))
                continue;

            var words = text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', '!', '?', ':', ';', '(', ')', '[', ']', '{', '}' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3) // Skip short words
                .Select(w => w.ToLowerInvariant())
                .Where(w => !IsStopWord(w));

            foreach (var word in words)
            {
                wordCounts[word] = wordCounts.GetValueOrDefault(word, 0) + 1;
            }
        }

        // Return top words by frequency
        return wordCounts
            .OrderByDescending(kv => kv.Value)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();
    }

    private static bool IsStopWord(string word)
    {
        // Filter unicode escapes, special characters, and very short words
        if (word.Length < 3 ||
            word.StartsWith("\\u") ||
            word.StartsWith("\\r") ||
            word.StartsWith("\\n") ||
            word.Contains("\"") ||
            word.Contains("'") ||
            word.StartsWith("<") ||
            word.StartsWith("http") ||
            word.All(c => char.IsDigit(c)))
        {
            return true;
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Articles, conjunctions, prepositions
            "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
            "of", "with", "by", "from", "as", "is", "was", "are", "were", "been",
            "be", "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "need", "dare", "ought",
            "used", "this", "that", "these", "those", "into", "onto", "upon", "about",
            // Pronouns
            "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them",
            "my", "your", "his", "its", "our", "their", "mine", "yours", "hers", "ours", "theirs",
            "myself", "yourself", "himself", "herself", "itself", "ourselves", "themselves",
            // Question words
            "what", "which", "who", "whom", "whose", "when", "where", "why", "how",
            // Quantifiers
            "all", "each", "every", "both", "few", "more", "most", "other", "some",
            "such", "no", "nor", "not", "only", "own", "same", "so", "than", "too",
            "very", "just", "also", "now", "here", "there", "then", "once",
            // Common verbs
            "get", "got", "getting", "make", "made", "making", "take", "took", "taking",
            "come", "came", "coming", "go", "went", "going", "know", "knew", "knowing",
            "think", "thought", "see", "saw", "seeing", "want", "wanted", "wanting",
            "use", "using", "find", "found", "give", "gave", "tell", "told",
            "work", "working", "seem", "seemed", "feel", "felt", "try", "tried",
            "leave", "left", "call", "called", "keep", "kept", "let", "begin", "began",
            "show", "showed", "hear", "heard", "play", "played", "run", "ran", "move", "moved",
            // Common conversational words
            "like", "said", "says", "say", "please", "thanks", "thank", "okay", "yes", "yeah",
            "sure", "well", "really", "actually", "basically", "literally", "probably",
            "maybe", "something", "anything", "everything", "nothing", "someone", "anyone",
            "everyone", "thing", "things", "way", "ways", "time", "times", "day", "days",
            "year", "years", "people", "person", "place", "part", "good", "great", "new",
            "first", "last", "long", "little", "big", "high", "small", "large", "next",
            "early", "young", "important", "public", "bad", "right", "able", "back",
            // AI conversation artifacts
            "user", "assistant", "provided", "context", "model", "request", "response",
            "information", "example", "following", "based", "using", "help", "need",
            "want", "would", "could", "create", "generated", "output", "input",
            "related", "specific", "include", "including", "however", "therefore",
            "additionally", "furthermore", "moreover", "thus", "hence", "turn",
            // Common noise
            "null", "undefined", "true", "false", "string", "number", "object", "array",
            "function", "return", "const", "var", "let", "class", "public", "private",
            "void", "int", "bool", "data", "value", "values", "name", "names", "type", "types"
        };

        return stopWords.Contains(word);
    }

    private async Task<Dictionary<int, long>> SaveTopicsAsync(
        long runId,
        List<ExtractedTopic> topics)
    {
        var topicIdMap = new Dictionary<int, long>();

        foreach (var topic in topics)
        {
            var entity = new Topic
            {
                InferenceRunId = runId,
                Label = topic.Label,
                KeywordsJson = topic.Keywords.Any()
                    ? JsonSerializer.Serialize(topic.Keywords)
                    : null
            };

            _db.Topics.Add(entity);
            await _db.SaveChangesAsync();

            topicIdMap[topic.TopicIndex] = entity.TopicId;
        }

        return topicIdMap;
    }

    private async Task<int> SaveAssignmentsAsync(
        long runId,
        IReadOnlyList<TopicAssignment> assignments,
        Dictionary<int, long> topicIdMap,
        decimal minScoreThreshold)
    {
        var count = 0;

        foreach (var assignment in assignments.Where(a => a.Score >= minScoreThreshold))
        {
            if (!topicIdMap.TryGetValue(assignment.TopicIndex, out var topicId))
                continue;

            _db.ConversationTopics.Add(new ConversationTopic
            {
                InferenceRunId = runId,
                ConversationId = assignment.ConversationId,
                TopicId = topicId,
                Score = assignment.Score
            });

            count++;
        }

        await _db.SaveChangesAsync();
        return count;
    }

    private static IReadOnlyList<string> ParseKeywords(string? keywordsJson)
    {
        if (string.IsNullOrEmpty(keywordsJson))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<string>>(keywordsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
