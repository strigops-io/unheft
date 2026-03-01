using Unheft.Services;

namespace Unheft.Tests;

public class DesignerParserTests
{
    [Fact]
    public void ExtractAttributes_ExtractsDbContextAndMigration()
    {
        var result = DesignerParser.ExtractAttributes(TestFixtures.DesignerFile);

        Assert.NotNull(result);
        Assert.Contains("DbContext", result.DbContextAttribute);
        Assert.Contains("UnheftDemoDbContext", result.DbContextAttribute);
        Assert.Contains("Migration", result.MigrationAttribute);
        Assert.Contains("20251215053906_MgRemoveCustomerEmailJourney", result.MigrationAttribute);
    }

    [Fact]
    public void ExtractAttributes_ReturnsNullForMissingAttributes()
    {
        var result = DesignerParser.ExtractAttributes(TestFixtures.DesignerFileNoAttributes);

        Assert.Null(result);
    }

    [Fact]
    public void HasMigrationAttributes_ReturnsFalseForPlainMigration()
    {
        var result = DesignerParser.HasMigrationAttributes(TestFixtures.MainMigrationFile);

        Assert.False(result);
    }

    [Fact]
    public void HasMigrationAttributes_ReturnsTrueForConsolidatedFile()
    {
        var result = DesignerParser.HasMigrationAttributes(TestFixtures.AlreadyConsolidatedFile);

        Assert.True(result);
    }

    [Fact]
    public void ExtractAttributes_ReturnsNullForEmptyContent()
    {
        var result = DesignerParser.ExtractAttributes("");

        Assert.Null(result);
    }
}
