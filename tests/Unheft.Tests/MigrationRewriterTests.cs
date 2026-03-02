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

    [Fact]
    public void Consolidate_AddsDbContextNamespaceUsing()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        Assert.Contains("using UnheftDemo.Data;", result);
    }

    [Fact]
    public void Consolidate_TransfersDesignerUsingDirectives()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        // All using directives from the Designer file should be present
        Assert.Contains("using Microsoft.EntityFrameworkCore;", result);
        Assert.Contains("using Microsoft.EntityFrameworkCore.Infrastructure;", result);
        Assert.Contains("using Microsoft.EntityFrameworkCore.Migrations;", result);
        Assert.Contains("using UnheftDemo.Data;", result);
    }

    [Fact]
    public void Consolidate_DoesNotDuplicateExistingUsings()
    {
        var attributes = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile)!;

        var result = MigrationRewriter.Consolidate(TestFixtures.MainMigrationFile, attributes);

        // Microsoft.EntityFrameworkCore.Migrations is already in the main file;
        // it should appear exactly once
        var count = result.Split("using Microsoft.EntityFrameworkCore.Migrations;").Length - 1;
        Assert.Equal(1, count);
    }
}
