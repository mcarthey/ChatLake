namespace ChatLake.Core.Parsing;

/// <summary>
/// Resolves the appropriate parser for a raw artifact.
/// </summary>
public interface IRawArtifactParserResolver
{
    IRawArtifactParser Resolve(
        string artifactType,
        string artifactName);
}
