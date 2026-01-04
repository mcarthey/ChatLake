using ChatLake.Core.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ChatLake.Infrastructure.Parsing.ChatGpt;

/// <summary>
/// Parses ChatGPT export "conversations.json" (OpenAI data export format).
/// Pure parser: no DB, no side effects.
/// </summary>
public sealed class ChatGptConversationsJsonParser : IRawArtifactParser
{
    public async IAsyncEnumerable<ParsedConversation> ParseAsync(
        Stream jsonStream,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var doc = await JsonDocument.ParseAsync(
            jsonStream,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            },
            ct);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var convEl in doc.RootElement.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();

            var parsed = ParseSingleConversation(convEl);
            if (parsed != null)
                yield return parsed;
        }
    }

    private ParsedConversation? ParseSingleConversation(JsonElement convEl)
    {
        if (convEl.ValueKind != JsonValueKind.Object)
            return null;

        var externalId = GetString(convEl, "id");
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        if (!convEl.TryGetProperty("mapping", out var mappingEl)
            || mappingEl.ValueKind != JsonValueKind.Object)
            return null;

        var currentNodeId = GetString(convEl, "current_node");
        if (string.IsNullOrWhiteSpace(currentNodeId))
            return null;

        var nodes = ParseNodes(mappingEl);
        if (!nodes.TryGetValue(currentNodeId, out var currentNode))
            return null;

        // Walk from current node to root
        var chain = new List<Node>();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        var cursor = currentNode;
        while (cursor is not null)
        {
            if (!visited.Add(cursor.Id))
                break;

            chain.Add(cursor);

            cursor = cursor.ParentId != null && nodes.TryGetValue(cursor.ParentId, out var parent)
                ? parent
                : null;
        }

        chain.Reverse();

        var messages = ImmutableArray.CreateBuilder<ParsedMessage>();
        var seq = 0;

        foreach (var node in chain)
        {
            if (node.Message is null)
                continue;

            messages.Add(new ParsedMessage(
                Role: node.Message.Role,
                SequenceIndex: seq++,
                Content: node.Message.Content,
                MessageTimestampUtc: node.Message.CreatedAtUtc));
        }

        if (messages.Count == 0)
            return null;

        return new ParsedConversation(
            SourceSystem: "ChatGPT",
            ExternalConversationId: externalId,
            Messages: messages.ToImmutable());
    }

    // -----------------------------
    // Parsing helpers
    // -----------------------------

    private static Dictionary<string, Node> ParseNodes(JsonElement mappingEl)
    {
        var dict = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var prop in mappingEl.EnumerateObject())
        {
            var nodeId = prop.Name;
            var nodeEl = prop.Value;

            string? parentId = null;
            if (nodeEl.TryGetProperty("parent", out var parentEl)
                && parentEl.ValueKind == JsonValueKind.String)
            {
                parentId = parentEl.GetString();
            }

            ChatMessage? msg = null;
            if (nodeEl.TryGetProperty("message", out var msgEl)
                && msgEl.ValueKind == JsonValueKind.Object)
            {
                msg = ParseMessage(msgEl);
            }

            dict[nodeId] = new Node(nodeId, parentId, msg);
        }

        return dict;
    }

    private static ChatMessage? ParseMessage(JsonElement msgEl)
    {
        var role = ExtractRole(msgEl);
        var content = ExtractContent(msgEl);
        var createdAtUtc = ExtractCreatedAtUtc(msgEl);

        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(content))
            return null;

        return new ChatMessage(role, content, createdAtUtc);
    }

    private static string? ExtractRole(JsonElement msgEl)
    {
        if (!msgEl.TryGetProperty("author", out var authorEl)
            || authorEl.ValueKind != JsonValueKind.Object)
            return null;

        return GetString(authorEl, "role");
    }

    private static string? ExtractContent(JsonElement msgEl)
    {
        if (!msgEl.TryGetProperty("content", out var contentEl)
            || contentEl.ValueKind != JsonValueKind.Object)
            return null;

        // Primary: content.parts[]
        if (contentEl.TryGetProperty("parts", out var partsEl)
            && partsEl.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();

            foreach (var part in partsEl.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                    parts.Add(part.GetString() ?? string.Empty);
            }

            var joined = string.Join("\n", parts).Trim();
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        // Fallback
        var fallback = contentEl.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static DateTime? ExtractCreatedAtUtc(JsonElement msgEl)
    {
        if (!msgEl.TryGetProperty("create_time", out var ctEl))
            return null;

        double? seconds = ctEl.ValueKind switch
        {
            JsonValueKind.Number when ctEl.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(
                ctEl.GetString(),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var ds) => ds,
            _ => null
        };

        if (seconds is null)
            return null;

        try
        {
            return DateTimeOffset
                .FromUnixTimeSeconds((long)Math.Floor(seconds.Value))
                .UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;

        return el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : el.ToString();
    }

    // -----------------------------
    // Internal models
    // -----------------------------

    private sealed record Node(
        string Id,
        string? ParentId,
        ChatMessage? Message);

    private sealed record ChatMessage(
        string Role,
        string Content,
        DateTime? CreatedAtUtc);
}
