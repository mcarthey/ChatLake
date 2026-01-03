using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using ChatLake.Core.Parsing;

namespace ChatLake.Infrastructure.Parsing.ChatGpt;

/// <summary>
/// Parses ChatGPT export "conversations.json" (OpenAI data export format).
/// Pure parser: no DB, no side effects.
/// </summary>
public sealed class ChatGptConversationsJsonParser : IRawArtifactParser
{
    public IReadOnlyCollection<ParsedConversation> Parse(string rawJson)
    {
        if (rawJson is null) throw new ArgumentNullException(nameof(rawJson));

        using var doc = JsonDocument.Parse(rawJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<ParsedConversation>();

        var results = new List<ParsedConversation>();

        foreach (var convEl in doc.RootElement.EnumerateArray())
        {
            if (convEl.ValueKind != JsonValueKind.Object)
                continue;

            var externalId = GetString(convEl, "id");
            if (string.IsNullOrWhiteSpace(externalId))
                continue;

            if (!convEl.TryGetProperty("mapping", out var mappingEl) || mappingEl.ValueKind != JsonValueKind.Object)
                continue;

            var currentNodeId = GetString(convEl, "current_node");
            if (string.IsNullOrWhiteSpace(currentNodeId))
                continue;

            var nodes = ParseNodes(mappingEl);
            if (!nodes.TryGetValue(currentNodeId, out var currentNode))
                continue;

            // Walk from current node -> root via parent pointers; then reverse.
            var chain = new List<Node>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            var cursor = currentNode;
            while (cursor is not null)
            {
                if (cursor.Id is null || !visited.Add(cursor.Id))
                    break; // cycle protection

                chain.Add(cursor);

                cursor = (cursor.ParentId is not null && nodes.TryGetValue(cursor.ParentId, out var parent))
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

                var role = node.Message.Role;
                var content = node.Message.Content;

                if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(content))
                    continue;

                messages.Add(new ParsedMessage(
                    Role: role,
                    SequenceIndex: seq++,
                    Content: content,
                    MessageTimestampUtc: node.Message.CreatedAtUtc));
            }

            // If there are no messages, still allow a conversation record? For now: skip empty.
            if (messages.Count == 0)
                continue;

            results.Add(new ParsedConversation(
                SourceSystem: "ChatGPT",
                ExternalConversationId: externalId,
                Messages: messages.ToImmutable()));

        }

        return results;
    }

    private static Dictionary<string, Node> ParseNodes(JsonElement mappingEl)
    {
        var dict = new Dictionary<string, Node>(StringComparer.Ordinal);

        foreach (var prop in mappingEl.EnumerateObject())
        {
            var nodeId = prop.Name;
            var nodeEl = prop.Value;

            string? parentId = null;
            if (nodeEl.ValueKind == JsonValueKind.Object && nodeEl.TryGetProperty("parent", out var parentEl))
            {
                parentId = parentEl.ValueKind == JsonValueKind.String ? parentEl.GetString() : null;
            }

            ChatMessage? msg = null;
            if (nodeEl.ValueKind == JsonValueKind.Object && nodeEl.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.Object)
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

        // Keep messages that have at least role + content; others treated as non-message nodes.
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(content))
            return null;

        return new ChatMessage(role, content, createdAtUtc);
    }

    private static string? ExtractRole(JsonElement msgEl)
    {
        if (!msgEl.TryGetProperty("author", out var authorEl) || authorEl.ValueKind != JsonValueKind.Object)
            return null;

        var role = GetString(authorEl, "role");
        return string.IsNullOrWhiteSpace(role) ? null : role;
    }

    private static string? ExtractContent(JsonElement msgEl)
    {
        if (!msgEl.TryGetProperty("content", out var contentEl) || contentEl.ValueKind != JsonValueKind.Object)
            return null;

        // Most common: content.parts (array of strings)
        if (contentEl.TryGetProperty("parts", out var partsEl) && partsEl.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var part in partsEl.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.String)
                    parts.Add(part.GetString() ?? string.Empty);
                else
                    parts.Add(part.ToString());
            }

            var joined = string.Join("\n", parts).Trim();
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }

        // Fallback: some exports contain other shapes; use a stable string form
        var fallback = contentEl.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }

    private static DateTime? ExtractCreatedAtUtc(JsonElement msgEl)
    {
        // create_time typically seconds since epoch, sometimes floating point
        if (!msgEl.TryGetProperty("create_time", out var ctEl))
            return null;

        double? seconds = ctEl.ValueKind switch
        {
            JsonValueKind.Number when ctEl.TryGetDouble(out var d) => d,
            JsonValueKind.String when double.TryParse(ctEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ds) => ds,
            _ => null
        };

        if (seconds is null)
            return null;

        try
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds((long)Math.Floor(seconds.Value));
            return dto.UtcDateTime;
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

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private sealed record Node(string Id, string? ParentId, ChatMessage? Message);

    private sealed record ChatMessage(string Role, string Content, DateTime? CreatedAtUtc);
}
