using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unheft.Services;

/// <summary>
/// Rewrites EF Core migration files to include Designer attributes and a BuildTargetModel stub.
/// </summary>
public static class MigrationRewriter
{
    /// <summary>
    /// Adds the [DbContext] and [Migration] attributes and a BuildTargetModel stub
    /// to a migration file's content.
    /// </summary>
    public static string Consolidate(string mainFileContent, DesignerAttributes attributes)
    {
        var tree = CSharpSyntaxTree.ParseText(mainFileContent);
        var root = tree.GetCompilationUnitRoot();

        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();

        if (classDeclaration is null)
            throw new InvalidOperationException("No class declaration found in migration file.");

        // Add using directives if not present
        root = EnsureUsing(root, "Microsoft.EntityFrameworkCore");
        root = EnsureUsing(root, "Microsoft.EntityFrameworkCore.Infrastructure");

        // Re-find class after potential tree modification
        classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        // Build clean attribute lists
        var dbContextAttrList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName(attributes.DbContextAttribute))));

        var migrationAttrList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.ParseName(attributes.MigrationAttribute))));

        // Extract only the immediate indentation trivia (last whitespace before the class keyword)
        var allTrivia = classDeclaration.GetLeadingTrivia();
        var lastWhitespace = allTrivia
            .LastOrDefault(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
        var indentTrivia = lastWhitespace == default
            ? SyntaxTriviaList.Empty
            : SyntaxTriviaList.Create(lastWhitespace);

        // Strip existing doc comments from the class leading trivia
        var classLeadingTrivia = classDeclaration.GetLeadingTrivia();

        // Add attribute lists with proper formatting before any doc comments
        var newAttrLists = classDeclaration.AttributeLists
            .Add(dbContextAttrList
                .WithLeadingTrivia(indentTrivia)
                .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed))
            .Add(migrationAttrList
                .WithLeadingTrivia(indentTrivia)
                .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed));

        var newClassDecl = classDeclaration
            .WithAttributeLists(newAttrLists)
            .WithLeadingTrivia(indentTrivia);

        // Add BuildTargetModel stub if not already present
        if (!HasBuildTargetModel(classDeclaration))
        {
            var stubMethod = ParseBuildTargetModelStub();
            newClassDecl = newClassDecl.AddMembers(stubMethod);
        }

        root = root.ReplaceNode(classDeclaration, newClassDecl);

        return root.ToFullString();
    }

    private static CompilationUnitSyntax EnsureUsing(CompilationUnitSyntax root, string namespaceName)
    {
        var hasUsing = root.Usings.Any(u => u.Name?.ToString() == namespaceName);
        if (hasUsing)
            return root;

        var newUsing = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(" " + namespaceName))
            .WithTrailingTrivia(SyntaxFactory.ElasticLineFeed);

        return root.AddUsings(newUsing);
    }

    private static bool HasBuildTargetModel(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<MethodDeclarationSyntax>()
            .Any(m => m.Identifier.Text == "BuildTargetModel");
    }

    private static MethodDeclarationSyntax ParseBuildTargetModelStub()
    {
        var code = @"class Dummy {
    /// <summary>
    /// Intentionally empty. The original Designer file has been archived by unheft.
    /// </summary>
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
    }
}";
        var tree = CSharpSyntaxTree.ParseText(code);
        return tree.GetCompilationUnitRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();
    }
}
