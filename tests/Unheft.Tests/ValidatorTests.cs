using Unheft.Services;

namespace Unheft.Tests;

public class ValidatorTests
{
    [Fact]
    public void CompareSql_ReturnsTrueForIdenticalSql()
    {
        var sql = "CREATE TABLE [Customers] ([Id] int NOT NULL);";

        var (isValid, diff) = Validator.CompareSql(sql, sql);

        Assert.True(isValid);
        Assert.Null(diff);
    }

    [Fact]
    public void CompareSql_ReturnsFalseForDifferentSql()
    {
        var before = "CREATE TABLE [Customers] ([Id] int NOT NULL);";
        var after = "CREATE TABLE [Orders] ([Id] int NOT NULL);";

        var (isValid, diff) = Validator.CompareSql(before, after);

        Assert.False(isValid);
        Assert.NotNull(diff);
        Assert.Contains("First difference", diff);
    }

    [Fact]
    public void CompareSql_HandlesEmptyStrings()
    {
        var (isValid, diff) = Validator.CompareSql("", "");

        Assert.True(isValid);
        Assert.Null(diff);
    }

    [Fact]
    public void CompareSql_HandlesDifferentLengths()
    {
        var before = "line1\nline2";
        var after = "line1\nline2\nline3";

        var (isValid, diff) = Validator.CompareSql(before, after);

        Assert.False(isValid);
        Assert.NotNull(diff);
    }
}
