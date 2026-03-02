using Unheft.Services;

namespace Unheft.Tests;

public class WizardTests
{
    [Fact]
    public void SearchForMigrationDirectories_FindsDirectoryWithDesignerFiles()
    {
        var tempDir = TestFixtures.CreateMigrationPairOnDisk();
        try
        {
            // The temp dir itself contains .Designer.cs files
            var candidates = Wizard.SearchForMigrationDirectories(tempDir);

            Assert.Single(candidates);
            Assert.Equal(tempDir, candidates[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchForMigrationDirectories_FindsWellKnownMigrationPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        var migrationsDir = Path.Combine(tempDir, "Migrations");
        Directory.CreateDirectory(migrationsDir);
        try
        {
            // Create a migration pair inside Migrations/
            TestFixtures.CreateMigrationPairOnDisk(migrationsDir);

            var candidates = Wizard.SearchForMigrationDirectories(tempDir);

            Assert.Contains(candidates, c => c == migrationsDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchForMigrationDirectories_ReturnsEmptyForNoMigrations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var candidates = Wizard.SearchForMigrationDirectories(tempDir);

            Assert.Empty(candidates);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SearchForMigrationDirectories_FindsNestedDirectories()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        var nestedDir = Path.Combine(tempDir, "src", "MyProject", "Migrations");
        Directory.CreateDirectory(nestedDir);
        try
        {
            TestFixtures.CreateMigrationPairOnDisk(nestedDir);

            var candidates = Wizard.SearchForMigrationDirectories(tempDir);

            Assert.Single(candidates);
            Assert.Equal(nestedDir, candidates[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task RunAsync_ShowsHelpWhenUserChooses2()
    {
        var input = new StringReader("2\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var result = await Wizard.RunAsync(input, output, error);

        Assert.Equal(-1, result);
        Assert.Contains("wizard", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("help", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ReturnsErrorForInvalidChoice()
    {
        var input = new StringReader("invalid\n");
        var output = new StringWriter();
        var error = new StringWriter();

        var result = await Wizard.RunAsync(input, output, error);

        Assert.Equal(1, result);
        Assert.Contains("Invalid choice", error.ToString());
    }
}
