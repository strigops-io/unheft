using Unheft.Services;

namespace Unheft.Tests;

public class MigrationDiscoveryTests
{
    [Fact]
    public void FindMigrationPairs_FindsPairInDirectory()
    {
        var tempDir = TestFixtures.CreateMigrationPairOnDisk();
        try
        {
            var pairs = MigrationDiscovery.FindMigrationPairs(tempDir);

            Assert.Single(pairs);
            Assert.Contains("MgRemoveCustomerEmailJourney.cs", pairs[0].MainFilePath);
            Assert.Contains("MgRemoveCustomerEmailJourney.Designer.cs", pairs[0].DesignerFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindMigrationPairs_FindsMultiplePairs()
    {
        var tempDir = TestFixtures.CreateMultipleMigrationPairsOnDisk();
        try
        {
            var pairs = MigrationDiscovery.FindMigrationPairs(tempDir);

            Assert.Equal(2, pairs.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindMigrationPairs_IgnoresOrphanedDesignerFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            // Only create a Designer file, no main file
            File.WriteAllText(
                Path.Combine(tempDir, "20251215053906_Orphan.Designer.cs"),
                TestFixtures.DesignerFile);

            var pairs = MigrationDiscovery.FindMigrationPairs(tempDir);

            Assert.Empty(pairs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindMigrationPairs_ReturnsEmptyForEmptyDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "unheft-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var pairs = MigrationDiscovery.FindMigrationPairs(tempDir);

            Assert.Empty(pairs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindMigrationPairs_ThrowsForMissingDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(
            () => MigrationDiscovery.FindMigrationPairs("/nonexistent/path"));
    }
}
