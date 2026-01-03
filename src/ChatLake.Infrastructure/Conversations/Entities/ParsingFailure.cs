using System;

namespace ChatLake.Infrastructure.Conversations.Entities;

public class ParsingFailure
{
    public long ParsingFailureId { get; set; }

    public long RawArtifactId { get; set; }

    public string FailureStage { get; set; } = null!;
    public string FailureMessage { get; set; } = null!;

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
