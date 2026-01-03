using System;

namespace ChatLake.Core.Parsing;

public sealed record ParsedMessage(
    string Role,
    int SequenceIndex,
    string Content,
    DateTime? MessageTimestampUtc);
