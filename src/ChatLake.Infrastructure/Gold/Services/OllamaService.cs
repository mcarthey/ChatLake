using System.Text;
using System.Text.Json;
using ChatLake.Core.Models;
using ChatLake.Core.Services;
using ChatLake.Infrastructure.Logging;
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
            ConsoleLog.Error("Ollama", $"Error generating name: {ex.Message}");
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
            ConsoleLog.Error("Ollama", $"Error generating embedding: {ex.Message}");
            return null;
        }
    }

    // ===== Blog Generation Methods =====

    public async Task<BlogEvaluationScores> EvaluateClusterForBlogAsync(
        IReadOnlyList<string> segmentTexts,
        string clusterName,
        CancellationToken cancellationToken = default)
    {
        if (segmentTexts.Count == 0)
        {
            return new BlogEvaluationScores
            {
                OverallScore = 0,
                Reasoning = "No content to evaluate"
            };
        }

        // Sample segments for evaluation (don't send entire cluster)
        var samples = segmentTexts.Take(10).Select(t => TruncateText(t, 600));
        var samplesText = string.Join("\n\n---\n\n", samples);

        var jsonFormat = """{"educationalValue":0.00,"problemSolvingDepth":0.00,"topicCoherence":0.00,"contentCompleteness":0.00,"readerInterest":0.00,"reasoning":"brief explanation"}""";
        var prompt = $"""
            You are evaluating whether a cluster of AI assistant conversations would make a good technical blog post for LearnedGeek.com, a site that makes complex topics approachable.

            Cluster Name: {clusterName}
            Number of Segments: {segmentTexts.Count}

            Sample Content:
            {samplesText}

            Evaluate this cluster on these criteria (score each 0.0 to 1.0):

            1. Educational Value: Does it teach something useful that developers would want to learn?
            2. Problem-Solving Depth: Is there a journey of debugging, troubleshooting, or iterating toward a solution? (High score = back-and-forth problem solving, errors encountered and fixed, multiple approaches tried. Low score = simple Q&A with immediate answers.)
            3. Topic Coherence: Is the content focused enough for a single blog post?
            4. Content Completeness: Is there enough material for a ~2000 word post?
            5. Reader Interest: Would developers find this interesting/valuable?

            IMPORTANT: These are AI assistant conversations where a user asked for help. Problem-solving depth is about the JOURNEY - did they debug errors, try different approaches, iterate on solutions? Most technical conversations involve SOME problem-solving.

            Return ONLY valid JSON in this exact format (no markdown, no explanation):
            {jsonFormat}
            """;

        try
        {
            var response = await GenerateTextAsync(prompt, temperature: 0.2f, maxTokens: 300, cancellationToken);

            // Extract JSON from response (handle markdown code blocks)
            var json = ExtractJson(response);

            var parsed = JsonSerializer.Deserialize<JsonElement>(json);

            var scores = new BlogEvaluationScores
            {
                EducationalValue = GetDecimalValue(parsed, "educationalValue"),
                ProblemSolvingDepth = GetDecimalValue(parsed, "problemSolvingDepth"),
                TopicCoherence = GetDecimalValue(parsed, "topicCoherence"),
                ContentCompleteness = GetDecimalValue(parsed, "contentCompleteness"),
                ReaderInterest = GetDecimalValue(parsed, "readerInterest"),
                Reasoning = parsed.TryGetProperty("reasoning", out var r) ? r.GetString() : null
            };

            // Calculate weighted overall score
            return scores with { OverallScore = BlogEvaluationScores.CalculateOverallScore(scores) };
        }
        catch (Exception ex)
        {
            ConsoleLog.Error("Ollama", $"Error evaluating cluster: {ex.Message}");
            return new BlogEvaluationScores
            {
                OverallScore = 0,
                Reasoning = $"Evaluation failed: {ex.Message}"
            };
        }
    }

    public async Task<string> GenerateBlogTitleAsync(
        IReadOnlyList<string> segmentTexts,
        string clusterName,
        CancellationToken cancellationToken = default)
    {
        var samples = segmentTexts.Take(5).Select(t => TruncateText(t, 400));
        var samplesText = string.Join("\n---\n", samples);

        var prompt = $"""
            Create a compelling blog post title for LearnedGeek.com based on this AI conversation cluster.

            Cluster Topic: {clusterName}

            Sample Content:
            {samplesText}

            The title should be:
            - Engaging and specific (not generic like "A Guide to X")
            - Written from the author's perspective (learning journey)
            - Hint at the problem solved or lesson learned
            - Appeal to developers who might face similar challenges

            Good examples:
            - "How I Finally Understood React Server Components"
            - "The Debugging Session That Taught Me About Memory Leaks"
            - "Building a Real-Time Chat: Lessons from 3 Failed Attempts"

            Return ONLY the title, nothing else. No quotes.
            """;

        try
        {
            var response = await GenerateTextAsync(prompt, temperature: 0.7f, maxTokens: 50, cancellationToken);
            return response.Trim().Trim('"', '\'');
        }
        catch (Exception ex)
        {
            ConsoleLog.Error("Ollama", $"Error generating title: {ex.Message}");
            return $"Deep Dive: {clusterName}";
        }
    }

    public async Task<BlogOutline> GenerateBlogOutlineAsync(
        IReadOnlyList<string> segmentTexts,
        string title,
        CancellationToken cancellationToken = default)
    {
        var samples = segmentTexts.Take(8).Select(t => TruncateText(t, 500));
        var samplesText = string.Join("\n\n---\n\n", samples);

        var outlineJsonFormat = """{"hook":"one sentence hook","sections":[{"heading":"Section Title","keyPoints":["point 1","point 2"]}],"keyTakeaways":["takeaway 1","takeaway 2"],"codeExamples":["brief description of code to include"]}""";
        var prompt = $"""
            Create a structured outline for a blog post titled: "{title}"

            Source Content:
            {samplesText}

            Create an outline that follows a story arc:
            1. Hook/Problem - What challenge or question prompted this journey?
            2. Context/Background - What should readers understand first?
            3. The Journey - Key steps, discoveries, or attempts
            4. Solution/Resolution - What worked and why?
            5. Takeaways - What can readers apply to their own work?

            Return ONLY valid JSON (no markdown, no explanation):
            {outlineJsonFormat}
            """;

        try
        {
            var response = await GenerateTextAsync(prompt, temperature: 0.5f, maxTokens: 800, cancellationToken);
            var json = ExtractJson(response);
            var parsed = JsonSerializer.Deserialize<JsonElement>(json);

            var sections = new List<BlogSection>();
            if (parsed.TryGetProperty("sections", out var sectionsArray))
            {
                foreach (var section in sectionsArray.EnumerateArray())
                {
                    var keyPoints = new List<string>();
                    if (section.TryGetProperty("keyPoints", out var points))
                    {
                        foreach (var point in points.EnumerateArray())
                        {
                            keyPoints.Add(point.GetString() ?? "");
                        }
                    }

                    sections.Add(new BlogSection
                    {
                        Heading = section.TryGetProperty("heading", out var h) ? h.GetString() ?? "" : "",
                        KeyPoints = keyPoints
                    });
                }
            }

            var takeaways = new List<string>();
            if (parsed.TryGetProperty("keyTakeaways", out var takeawaysArray))
            {
                foreach (var t in takeawaysArray.EnumerateArray())
                {
                    takeaways.Add(t.GetString() ?? "");
                }
            }

            var codeExamples = new List<string>();
            if (parsed.TryGetProperty("codeExamples", out var codeArray))
            {
                foreach (var c in codeArray.EnumerateArray())
                {
                    codeExamples.Add(c.GetString() ?? "");
                }
            }

            return new BlogOutline
            {
                Hook = parsed.TryGetProperty("hook", out var hook) ? hook.GetString() : null,
                Sections = sections,
                KeyTakeaways = takeaways,
                CodeExamples = codeExamples
            };
        }
        catch (Exception ex)
        {
            ConsoleLog.Error("Ollama", $"Error generating outline: {ex.Message}");
            return new BlogOutline
            {
                Sections =
                [
                    new BlogSection { Heading = "Introduction", KeyPoints = ["Set the context"] },
                    new BlogSection { Heading = "The Problem", KeyPoints = ["Describe the challenge"] },
                    new BlogSection { Heading = "The Solution", KeyPoints = ["Walk through the approach"] },
                    new BlogSection { Heading = "Conclusion", KeyPoints = ["Summarize learnings"] }
                ]
            };
        }
    }

    public async Task<string> GenerateBlogContentAsync(
        IReadOnlyList<string> segmentTexts,
        string title,
        BlogOutline outline,
        int targetWordCount,
        CancellationToken cancellationToken = default)
    {
        // Prepare source content - take more segments for content generation
        var sourceContent = string.Join("\n\n---\n\n",
            segmentTexts.Take(15).Select(t => TruncateText(t, 800)));

        // Serialize outline for prompt
        var outlineText = new StringBuilder();
        if (!string.IsNullOrEmpty(outline.Hook))
            outlineText.AppendLine($"Hook: {outline.Hook}");

        foreach (var section in outline.Sections)
        {
            outlineText.AppendLine($"\n## {section.Heading}");
            foreach (var point in section.KeyPoints)
            {
                outlineText.AppendLine($"- {point}");
            }
        }

        if (outline.KeyTakeaways.Any())
        {
            outlineText.AppendLine("\nKey Takeaways:");
            foreach (var t in outline.KeyTakeaways)
            {
                outlineText.AppendLine($"- {t}");
            }
        }

        var prompt = $"""
            Write a complete blog post for LearnedGeek.com.

            Title: {title}

            Outline:
            {outlineText}

            Source Material (from AI conversations):
            {sourceContent}

            STYLE GUIDE for LearnedGeek.com:
            - Write in first person - share YOUR journey of discovery
            - Conversational but technical - like explaining to a smart colleague
            - Educator mindset: "Let me walk you through..." not "You should know..."
            - NEVER condescending - assume the reader is smart but unfamiliar
            - Story arc: Problem → Journey → Solution → Lessons
            - Include relevant code snippets with clear explanations
            - Use analogies to make complex concepts approachable
            - Be specific with examples, avoid vague generalizations

            FORMAT:
            - Markdown format
            - Target: ~{targetWordCount} words
            - Use ## for section headers
            - Use ```language for code blocks
            - Include a brief intro paragraph before diving in

            Write the complete blog post now. Start with a compelling opening paragraph (no title - it's already set).
            """;

        try
        {
            var response = await GenerateTextAsync(
                prompt,
                temperature: 0.7f,
                maxTokens: targetWordCount * 2, // Allow room for markdown
                cancellationToken,
                timeout: TimeSpan.FromMinutes(5)); // Longer timeout for content generation

            return response.Trim();
        }
        catch (Exception ex)
        {
            ConsoleLog.Error("Ollama", $"Error generating blog content: {ex.Message}");
            return $"# {title}\n\n*Content generation failed: {ex.Message}*";
        }
    }

    // ===== Helper Methods =====

    private async Task<string> GenerateTextAsync(
        string prompt,
        float temperature,
        int maxTokens,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        // Default timeout of 3 minutes for most operations, 5 minutes for content generation
        timeout ??= TimeSpan.FromMinutes(3);

        using var timeoutCts = new CancellationTokenSource(timeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var responseBuilder = new StringBuilder();
            await foreach (var chunk in _client.GenerateAsync(new OllamaSharp.Models.GenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Options = new OllamaSharp.Models.RequestOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            }, linkedCts.Token))
            {
                responseBuilder.Append(chunk.Response);
            }
            return responseBuilder.ToString();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException($"LLM request timed out after {timeout.Value.TotalMinutes:F1} minutes");
        }
    }

    private static string ExtractJson(string response)
    {
        // Handle markdown code blocks
        if (response.Contains("```json"))
        {
            var start = response.IndexOf("```json") + 7;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response[start..end].Trim();
        }

        if (response.Contains("```"))
        {
            var start = response.IndexOf("```") + 3;
            var end = response.IndexOf("```", start);
            if (end > start)
                return response[start..end].Trim();
        }

        // Try to find JSON object
        var jsonStart = response.IndexOf('{');
        var jsonEnd = response.LastIndexOf('}');
        if (jsonStart >= 0 && jsonEnd > jsonStart)
        {
            return response[jsonStart..(jsonEnd + 1)];
        }

        return response.Trim();
    }

    private static decimal GetDecimalValue(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.Number
                ? (decimal)prop.GetDouble()
                : 0m;
        }
        return 0m;
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
