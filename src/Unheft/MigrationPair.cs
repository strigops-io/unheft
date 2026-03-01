namespace Unheft;

/// <summary>
/// Represents a discovered EF Core migration pair (main file + Designer file).
/// </summary>
public sealed record MigrationPair(string MainFilePath, string DesignerFilePath);
