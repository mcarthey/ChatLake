using System.Text;
using ChatLake.Core.Services;
using OllamaSharp;

namespace ChatLake.Infrastructure.Gold.Services;

/// <summary>
/// LLM service implementation using Ollama.
/// </summary>
public sealed class OllamaService : ILlmService
{
    private readonly OllamaApiClient _client;
    private readonly string _model;
    private readonly string _embeddingModel;

    public OllamaService(
        string baseUrl = "http://localhost:11434",
        string model = "mistral:7b",
        string embeddingModel = "nomic-embed-text")
    {
        _client = new OllamaApiClient(new Uri(baseUrl));
        _model = model;
        _embeddingModel = embeddingModel;
    }

    public async Task<string> GenerateClusterNameAsync(
        IReadOnlyList<string> sampleTexts,
        int conversationCount,
        CancellationToken cancellationToken = default)
    {
        if (sampleTexts.Count == 0)
            return $"Group ({conversationCount} conversations)";

        // Build prompt with sample snippets
        var samplesText = string.Join("\n---\n", sampleTexts.Select(t => TruncateText(t, 400)));

        var prompt = $"""
            Analyze these {sampleTexts.Count} conversation excerpts and identify what they have IN COMMON:

            {samplesText}

            What is the COMMON THEME that connects ALL of these conversations? Think about:
            - What domain or subject area do they ALL relate to?
            - What activity or goal unites them?
            - Is there a specific technology, hobby, project, or topic they ALL share?

            Now provide a SHORT name (2-5 words) for this theme.

            Rules:
            - Return ONLY the theme name, nothing else
            - No quotes, no explanation, no punctuation
            - The name must apply to ALL samples, not just one or two
            - Be specific but inclusive (e.g., "Home Electronics Setup" not "WiFi Configuration" if samples cover various electronics)
            - If samples are about a specific project/game/product, name it (e.g., "Black Ember Story Development")
            - AVOID generic names like "Technical Questions", "User Help", "Various Topics"
            """;

        try
        {
            // Collect streamed response
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _client.GenerateAsync(new OllamaSharp.Models.GenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    Temperature = 0.3f, // Low temperature for consistent naming
                    NumPredict = 30     // Short response
                }
            }, cancellationToken))
            {
                responseBuilder.Append(chunk.Response);
            }

            var name = responseBuilder.ToString().Trim();

            // Clean up response - remove quotes, periods, newlines
            name = name.Trim('"', '\'', '.', '\n', '\r');

            // Take only first line if multiple lines returned
            var firstLine = name.Split('\n')[0].Trim();
            if (!string.IsNullOrEmpty(firstLine))
                name = firstLine;

            // Fallback if response is empty, too long, or generic
            if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
                return $"Cluster {conversationCount}";

            // Catch if LLM ignored instruction and returned generic name
            if (name.StartsWith("Group", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("conversation", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Miscellaneous", StringComparison.OrdinalIgnoreCase))
                return $"Mixed Topics {conversationCount}";

            return name;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama] Error generating name: {ex.Message}");
            return $"Cluster {conversationCount}";
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);
            return models.Any(m => m.Name == _model || m.Name.StartsWith(_model.Split(':')[0]));
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsEmbeddingAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var models = await _client.ListLocalModelsAsync(cancellationToken).ConfigureAwait(false);
            return models.Any(m => m.Name == _embeddingModel || m.Name.StartsWith(_embeddingModel.Split(':')[0]));
        }
        catch
        {
            return false;
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            // Truncate text to fit embedding model context
            // nomic-embed-text has ~2048 token context, being conservative
            var truncatedText = TruncateText(text, 3000);

            var response = await _client.EmbedAsync(new OllamaSharp.Models.EmbedRequest
            {
                Model = _embeddingModel,
                Input = [truncatedText]
            }, cancellationToken).ConfigureAwait(false);

            if (response?.Embeddings != null && response.Embeddings.Count > 0)
            {
                // Convert double[] to float[] for ML.NET compatibility
                return response.Embeddings[0].Select(d => (float)d).ToArray();
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ollama] Error generating embedding: {ex.Message}");
            return null;
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        // Take first N characters, try to break at word boundary
        if (text.Length <= maxLength)
            return text;

        var truncated = text[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength / 2)
            truncated = truncated[..lastSpace];

        return truncated + "...";
    }
}
