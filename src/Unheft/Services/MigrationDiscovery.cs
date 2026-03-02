namespace Unheft.Services;

/// <summary>
/// Discovers EF Core migration pairs (main .cs file + .Designer.cs file) in a directory.
/// </summary>
public static class MigrationDiscovery
{
    private const string DesignerSuffix = ".Designer.cs";

    /// <summary>
    /// Finds all migration pairs in the given directory and its subdirectories.
    /// A migration pair consists of a main .cs file and its corresponding .Designer.cs file.
    /// </summary>
    public static IReadOnlyList<MigrationPair> FindMigrationPairs(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        var designerFiles = Directory.GetFiles(directory, "*.Designer.cs", SearchOption.AllDirectories);
        var pairs = new List<MigrationPair>();

        foreach (var designerFile in designerFiles)
        {
            // The main file is the Designer file path without the ".Designer" part
            var mainFile = designerFile[..^DesignerSuffix.Length] + ".cs";

            if (File.Exists(mainFile))
            {
                pairs.Add(new MigrationPair(mainFile, designerFile));
            }
        }

        return pairs;
    }
}
