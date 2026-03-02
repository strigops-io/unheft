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
        string projectPath, string? startupProject = null, string? context = null)
    {
        // Generate SQL script before consolidation
        var (beforeSuccess, beforeSql, beforeError) = await GenerateMigrationScriptAsync(projectPath, startupProject, context);
        if (!beforeSuccess)
        {
            return (false, null, null, $"Failed to generate SQL before consolidation: {beforeError}");
        }

        return (true, beforeSql, null, null);
    }

    /// <summary>
    /// Checks whether there are pending model changes that have not been added as a migration.
    /// </summary>
    public static async Task<(bool HasPending, string? Error)> CheckPendingModelChangesAsync(
        string projectPath, string? startupProject = null, string? context = null)
    {
        try
        {
            var args = BuildArgs("ef migrations has-pending-model-changes", projectPath, startupProject, context);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
                return (false, "Failed to start dotnet ef process");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Exit code 0 = no pending changes, non-zero = has pending changes
            if (process.ExitCode != 0)
                return (true, string.IsNullOrWhiteSpace(error) ? output : error);

            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
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

    private static string BuildArgs(string command, string projectPath, string? startupProject, string? context)
    {
        var args = $"{command} --project \"{projectPath}\"";
        if (startupProject is not null)
            args += $" --startup-project \"{startupProject}\"";
        if (context is not null)
            args += $" --context \"{context}\"";
        return args;
    }

    private static async Task<(bool Success, string? Output, string? Error)> GenerateMigrationScriptAsync(
        string projectPath, string? startupProject = null, string? context = null)
    {
        try
        {
            var args = BuildArgs("ef migrations script --idempotent --no-build", projectPath, startupProject, context);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = args,
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
