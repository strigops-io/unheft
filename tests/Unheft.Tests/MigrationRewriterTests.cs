using Unheft.Services;

namespace Unheft.Tests;

public class MigrationRewriterTests
{
    [Fact]
    public void Consolidate_AddsAttributesToMainFile()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        Assert.Contains("[DbContext(typeof(UnheftDemoDbContext))]", result);
        Assert.Contains("[Migration(\"20251215053906_MgRemoveCustomerEmailJourney\")]", result);
    }

    [Fact]
    public void Consolidate_AddsBuildTargetModelStub()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        Assert.Contains("BuildTargetModel", result);
        Assert.Contains("Intentionally empty", result);
    }

    [Fact]
    public void Consolidate_PreservesExistingCode()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        // Original Up and Down methods should still be present
        Assert.Contains("protected override void Up(MigrationBuilder migrationBuilder)", result);
        Assert.Contains("protected override void Down(MigrationBuilder migrationBuilder)", result);
        Assert.Contains("DropColumn", result);
        Assert.Contains("AddColumn", result);
    }

    [Fact]
    public void Consolidate_AddsInfrastructureUsing()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        Assert.Contains("using Microsoft.EntityFrameworkCore.Infrastructure;", result);
    }

    [Fact]
    public void Consolidate_ProducesCompilableOutput()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        // Parse with Roslyn to check it's valid C#
        var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(result);
        var diagnostics = tree.GetDiagnostics().Where(d =>
            d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);

        Assert.Empty(diagnostics);
    }
}
