namespace Unheft.Services;

/// <summary>
/// Result of a consolidation operation on a single migration pair.
/// </summary>
public sealed record ConsolidationResult(
    string MainFilePath,
    string DesignerFilePath,
    bool WasSkipped,
    string? SkipReason,
    string? ArchivedPath);

/// <summary>
/// Orchestrates the consolidation of EF Core migration Designer files.
/// </summary>
public static class Consolidator
{
    /// <summary>
    /// Consolidates all migration pairs found in the given directory.
    /// </summary>
    public static IReadOnlyList<ConsolidationResult> Run(string directory, bool dryRun = false)
    {
        var pairs = MigrationDiscovery.FindMigrationPairs(directory);
        var results = new List<ConsolidationResult>();

        foreach (var pair in pairs)
        {
            results.Add(ConsolidatePair(pair, dryRun));
        }

        return results;
    }

    /// <summary>
    /// Consolidates a single migration pair.
    /// </summary>
    public static ConsolidationResult ConsolidatePair(MigrationPair pair, bool dryRun = false)
    {
        var mainContent = File.ReadAllText(pair.MainFilePath);
        var designerContent = File.ReadAllText(pair.DesignerFilePath);

        // Idempotency: skip if already consolidated
        if (DesignerParser.HasMigrationAttributes(mainContent))
        {
            return new ConsolidationResult(
                pair.MainFilePath,
                pair.DesignerFilePath,
                WasSkipped: true,
                SkipReason: "Already consolidated (attributes present in main file)",
                ArchivedPath: null);
        }

        // Extract attributes from Designer file
        var attributes = DesignerParser.ExtractAttributes(designerContent);
        if (attributes is null)
        {
            return new ConsolidationResult(
                pair.MainFilePath,
                pair.DesignerFilePath,
                WasSkipped: true,
                SkipReason: "Could not extract [DbContext] and [Migration] attributes from Designer file",
                ArchivedPath: null);
        }

        if (dryRun)
        {
            return new ConsolidationResult(
                pair.MainFilePath,
                pair.DesignerFilePath,
                WasSkipped: false,
                SkipReason: null,
                ArchivedPath: pair.DesignerFilePath + DesignerArchiver.ArchiveExtension);
        }

        // Rewrite the main migration file
        var rewrittenContent = MigrationRewriter.Consolidate(mainContent, attributes);
        File.WriteAllText(pair.MainFilePath, rewrittenContent);

        // Archive the Designer file
        var archivedPath = DesignerArchiver.Archive(pair.DesignerFilePath);

        return new ConsolidationResult(
            pair.MainFilePath,
            pair.DesignerFilePath,
            WasSkipped: false,
            SkipReason: null,
            ArchivedPath: archivedPath);
    }
}
