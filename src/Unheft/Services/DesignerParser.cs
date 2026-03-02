using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unheft.Services;

/// <summary>
/// Parses EF Core migration Designer files to extract [DbContext] and [Migration] attributes.
/// </summary>
public static class DesignerParser
{
    /// <summary>
    /// Extracts the [DbContext(...)] and [Migration("...")] attribute expressions from a Designer file.
    /// Returns the attribute names with arguments (e.g. "DbContext(typeof(MyDbContext))" and "Migration(\"...\")").
    /// </summary>
    public static DesignerAttributes? ExtractAttributes(string designerFileContent)
    {
        var tree = CSharpSyntaxTree.ParseText(designerFileContent);
        var root = tree.GetCompilationUnitRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return null;

        string? dbContextAttr = null;
        string? migrationAttr = null;

        foreach (var attrList in classDeclaration.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();

                if (name is "DbContext" or "DbContextAttribute")
                {
                    // Store the full attribute text e.g. "DbContext(typeof(UnheftDemoDbContext))"
                    dbContextAttr = attr.ToString();
                }
                else if (name is "Migration" or "MigrationAttribute")
                {
                    migrationAttr = attr.ToString();
                }
            }
        }

        if (dbContextAttr is null || migrationAttr is null)
            return null;

        return new DesignerAttributes(dbContextAttr, migrationAttr);
    }

    /// <summary>
    /// Checks whether a migration file already has [DbContext] and [Migration] attributes,
    /// indicating it has already been consolidated.
    /// </summary>
    public static bool HasMigrationAttributes(string fileContent)
    {
        var tree = CSharpSyntaxTree.ParseText(fileContent);
        var root = tree.GetCompilationUnitRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            return false;

        bool hasDbContext = false;
        bool hasMigration = false;

        foreach (var attrList in classDeclaration.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name is "DbContext" or "DbContextAttribute")
                    hasDbContext = true;
                else if (name is "Migration" or "MigrationAttribute")
                    hasMigration = true;
            }
        }

        return hasDbContext && hasMigration;
    }
}
