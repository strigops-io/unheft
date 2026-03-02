using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unheft.Services;

/// <summary>
/// Detects project configuration relevant to EF Core migrations,
/// including project type, startup projects, and DbContext names.
/// </summary>
public static class ProjectDetector
{
    /// <summary>
    /// Finds the .csproj file by walking up from the migration directory.
    /// </summary>
    public static string? FindProjectFile(string migrationDirectory)
    {
        var dir = new DirectoryInfo(migrationDirectory);
        while (dir is not null)
        {
            var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
            if (csproj is not null)
                return csproj.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Determines if a project is a class library (non-runnable).
    /// A project is a class library if it has no OutputType or OutputType is "Library".
    /// </summary>
    public static bool IsClassLibrary(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var outputType = doc.Descendants("OutputType").FirstOrDefault()?.Value;
            return outputType is null || outputType.Equals("Library", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds projects that reference the given project, which could serve as startup projects.
    /// Searches sibling directories of the project's parent directory.
    /// </summary>
    public static IReadOnlyList<string> FindStartupProjects(string projectFilePath)
    {
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(projectFilePath))!;
        var results = new List<string>();

        var parentDir = Path.GetDirectoryName(projectDir);
        if (parentDir is null) return results;

        try
        {
            foreach (var siblingDir in Directory.GetDirectories(parentDir))
            {
                if (string.Equals(Path.GetFullPath(siblingDir), Path.GetFullPath(projectDir), StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var csproj in Directory.GetFiles(siblingDir, "*.csproj", SearchOption.TopDirectoryOnly))
                {
                    if (ReferencesProject(csproj, projectFilePath))
                    {
                        results.Add(csproj);
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore directories we can't access
        }

        return results;
    }

    /// <summary>
    /// Extracts distinct DbContext class names from Designer files in the migration directory.
    /// </summary>
    public static IReadOnlyList<string> DetectDbContextNames(string migrationDirectory)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var designerFiles = Directory.GetFiles(migrationDirectory, "*.Designer.cs", SearchOption.TopDirectoryOnly);
            foreach (var file in designerFiles)
            {
                var content = File.ReadAllText(file);
                var name = ExtractDbContextName(content);
                if (name is not null)
                    names.Add(name);
            }
        }
        catch
        {
            // Ignore access errors
        }

        return names.ToList();
    }

    /// <summary>
    /// Extracts the DbContext class name from a Designer file's content.
    /// Parses the [DbContext(typeof(XxxDbContext))] attribute.
    /// </summary>
    public static string? ExtractDbContextName(string designerContent)
    {
        var tree = CSharpSyntaxTree.ParseText(designerContent);
        var root = tree.GetCompilationUnitRoot();

        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDecl is null) return null;

        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "DbContext" or "DbContextAttribute")
                {
                    var arg = attr.ArgumentList?.Arguments.FirstOrDefault();
                    if (arg?.Expression is TypeOfExpressionSyntax typeofExpr)
                    {
                        return typeofExpr.Type.ToString();
                    }
                }
            }
        }

        return null;
    }

    private static bool ReferencesProject(string csprojPath, string targetProjectPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var targetFileName = Path.GetFileName(targetProjectPath);

            foreach (var reference in doc.Descendants("ProjectReference"))
            {
                var include = reference.Attribute("Include")?.Value;
                if (include is null) continue;

                // Normalize path separators for cross-platform compatibility
                var normalized = include.Replace('\\', Path.DirectorySeparatorChar);
                var referencedFileName = Path.GetFileName(normalized);
                if (referencedFileName.Equals(targetFileName, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Also check resolved path
                var csprojDir = Path.GetDirectoryName(csprojPath)!;
                var resolvedPath = Path.GetFullPath(Path.Combine(csprojDir, normalized));
                if (resolvedPath.Equals(Path.GetFullPath(targetProjectPath), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return false;
    }
}
