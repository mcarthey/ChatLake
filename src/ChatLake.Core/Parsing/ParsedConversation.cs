namespace ChatLake.Core.Parsing;

public sealed record ParsedConversation(
    string SourceSystem,
    string? ExternalConversationId,
    IReadOnlyList<ParsedMessage> Messages);
