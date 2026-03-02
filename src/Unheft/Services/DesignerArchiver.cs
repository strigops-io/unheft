namespace Unheft.Services;

/// <summary>
/// Archives Designer files by renaming them and excluding from compilation.
/// </summary>
public static class DesignerArchiver
{
    public const string ArchiveExtension = ".archived";

    /// <summary>
    /// Archives a Designer file by renaming it with an .archived extension.
    /// This effectively excludes it from C# compilation while preserving the original content.
    /// </summary>
    public static string Archive(string designerFilePath)
    {
        var archivedPath = designerFilePath + ArchiveExtension;

        if (File.Exists(archivedPath))
        {
            // Already archived — idempotent
            return archivedPath;
        }

        File.Move(designerFilePath, archivedPath);
        return archivedPath;
    }

    /// <summary>
    /// Checks whether a Designer file has already been archived.
    /// </summary>
    public static bool IsArchived(string designerFilePath)
    {
        return File.Exists(designerFilePath + ArchiveExtension);
    }
}
