using Unheft.Services;

namespace Unheft.Tests;

public class ConsolidatorTests
{
    [Fact]
    public void Run_ConsolidatesMigrationPair()
    {
        var tempDir = TestFixtures.CreateMigrationPairOnDisk();
        try
        {
            var results = Consolidator.Run(tempDir);

            Assert.Single(results);
            Assert.False(results[0].WasSkipped);
            Assert.NotNull(results[0].ArchivedPath);

            // Verify main file was modified
            var mainContent = File.ReadAllText(results[0].MainFilePath);
            Assert.Contains("[DbContext(typeof(UnheftDemoDbContext))]", mainContent);
            Assert.Contains("[Migration(\"20251215053906_MgRemoveCustomerEmailJourney\")]", mainContent);
            Assert.Contains("BuildTargetModel", mainContent);

            // Verify Designer file was archived
            Assert.True(File.Exists(results[0].ArchivedPath));
            Assert.False(File.Exists(results[0].DesignerFilePath));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_DryRunDoesNotModifyFiles()
    {
        var tempDir = TestFixtures.CreateMigrationPairOnDisk();
        try
        {
            var mainContentBefore = File.ReadAllText(
                Path.Combine(tempDir, "20251215053906_MgRemoveCustomerEmailJourney.cs"));

            var results = Consolidator.Run(tempDir, dryRun: true);

            Assert.Single(results);
            Assert.False(results[0].WasSkipped);

            // Verify files are unchanged
            var mainContentAfter = File.ReadAllText(
                Path.Combine(tempDir, "20251215053906_MgRemoveCustomerEmailJourney.cs"));
            Assert.Equal(mainContentBefore, mainContentAfter);

            // Designer file should still exist
            Assert.True(File.Exists(
                Path.Combine(tempDir, "20251215053906_MgRemoveCustomerEmailJourney.Designer.cs")));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_IsIdempotent()
    {
        var tempDir = TestFixtures.CreateMigrationPairOnDisk();
        try
        {
            // First run
            var results1 = Consolidator.Run(tempDir);
            Assert.Single(results1);
            Assert.False(results1[0].WasSkipped);

            var mainContentAfterFirst = File.ReadAllText(results1[0].MainFilePath);

            // The Designer file is now archived; create a new one to simulate re-run
            // But actually, the discovery should find only .Designer.cs files that still exist
            // Since the file is archived, there should be no pairs to discover
            var results2 = Consolidator.Run(tempDir);
            Assert.Empty(results2); // No more Designer.cs files to find

            // Main file should be unchanged
            var mainContentAfterSecond = File.ReadAllText(results1[0].MainFilePath);
            Assert.Equal(mainContentAfterFirst, mainContentAfterSecond);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_HandlesMultiplePairs()
    {
        var tempDir = TestFixtures.CreateMultipleMigrationPairsOnDisk();
        try
        {
            var results = Consolidator.Run(tempDir);

            Assert.Equal(2, results.Count);
            Assert.All(results, r => Assert.False(r.WasSkipped));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Run_ReturnsEmptyForDirectoryWithNoMigrations()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var results = Consolidator.Run(tempDir);

            Assert.Empty(results);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ConsolidatePair_SkipsWhenAlreadyConsolidated()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var mainPath = Path.Combine(tempDir, "20251215053906_MgRemoveCustomerEmailJourney.cs");
            var designerPath = Path.Combine(tempDir, "20251215053906_MgRemoveCustomerEmailJourney.Designer.cs");

            File.WriteAllText(mainPath, TestFixtures.AlreadyConsolidatedFile);
            File.WriteAllText(designerPath, TestFixtures.DesignerFile);

            var pair = new MigrationPair(mainPath, designerPath);
            var result = Consolidator.ConsolidatePair(pair);

            Assert.True(result.WasSkipped);
            Assert.Contains("Already consolidated", result.SkipReason);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
