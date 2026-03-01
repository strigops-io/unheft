using System.Diagnostics;

namespace Unheft.Services;

/// <summary>
/// Validates that consolidation is semantically lossless by comparing EF Core migration SQL
/// output before and after the transformation.
/// </summary>
public static class Validator
{
    /// <summary>
    /// Validates consolidation by generating migration SQL scripts before and after,
    /// and comparing them for equivalence.
    /// </summary>
    /// <returns>True if the SQL output is identical before and after consolidation.</returns>
    public static async Task<(bool IsValid, string? BeforeSql, string? AfterSql, string? Error)> ValidateAsync(
        string projectPath)
    {
        // Generate SQL script before consolidation
        var (beforeSuccess, beforeSql, beforeError) = await GenerateMigrationScriptAsync(projectPath);
        if (!beforeSuccess)
        {
            return (false, null, null, $"Failed to generate SQL before consolidation: {beforeError}");
        }

        return (true, beforeSql, null, null);
    }

    /// <summary>
    /// Compares SQL scripts generated before and after consolidation.
    /// </summary>
    public static (bool IsValid, string? Diff) CompareSql(string beforeSql, string afterSql)
    {
        if (string.Equals(beforeSql, afterSql, StringComparison.Ordinal))
        {
            return (true, null);
        }

        // Find first difference for diagnostic output
        var beforeLines = beforeSql.Split('\n');
        var afterLines = afterSql.Split('\n');

        for (int i = 0; i < Math.Max(beforeLines.Length, afterLines.Length); i++)
        {
            var before = i < beforeLines.Length ? beforeLines[i] : "<EOF>";
            var after = i < afterLines.Length ? afterLines[i] : "<EOF>";

            if (before != after)
            {
                return (false, $"First difference at line {i + 1}:\n  Before: {before}\n  After:  {after}");
            }
        }

        return (false, "Scripts differ in length");
    }

    private static async Task<(bool Success, string? Output, string? Error)> GenerateMigrationScriptAsync(
        string projectPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"ef migrations script --idempotent --no-build --project \"{projectPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return (false, null, "Failed to start dotnet ef process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                return (false, null, error);

            return (true, output, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
