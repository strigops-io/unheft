namespace Unheft.Services;

/// <summary>
/// Interactive wizard that guides users through the consolidation process
/// when unheft is run with no arguments.
/// </summary>
public static class Wizard
{
    private static readonly string[] CommonMigrationPaths =
    [
        "Migrations",
        "Data/Migrations",
        "Infrastructure/Migrations",
        "Persistence/Migrations",
        "Database/Migrations"
    ];

    /// <summary>
    /// Runs the interactive wizard, returning the exit code.
    /// </summary>
    public static async Task<int> RunAsync(TextReader input, TextWriter output, TextWriter error)
    {
        output.WriteLine();
        output.WriteLine("Welcome to unheft — EF Core Migration Designer File Consolidation Tool");
        output.WriteLine();
        output.WriteLine("  [1] Run the interactive wizard");
        output.WriteLine("  [2] Show help (--help)");
        output.WriteLine();
        output.Write("Choose an option (1/2): ");

        var choice = input.ReadLine()?.Trim();

        if (choice == "2")
        {
            return -1; // Signal to caller to show help
        }

        if (choice != "1")
        {
            error.WriteLine("Invalid choice. Run 'unheft --help' for usage information.");
            return 1;
        }

        return await RunWizardFlow(input, output, error);
    }

    private static async Task<int> RunWizardFlow(TextReader input, TextWriter output, TextWriter error)
    {
        output.WriteLine();

        // Step 1: Find migration directories
        var directory = await FindMigrationDirectory(input, output, error);
        if (directory is null)
            return 1;

        // Step 2: Ask about options
        output.WriteLine();
        output.Write("Run in dry-run mode? (preview changes without modifying files) [y/N]: ");
        var dryRun = IsYes(input.ReadLine());

        output.Write("Show verbose output? [y/N]: ");
        var verbose = IsYes(input.ReadLine());

        var validate = false;
        if (!dryRun)
        {
            output.Write("Validate that consolidation is semantically lossless? (requires dotnet ef tools) [y/N]: ");
            validate = IsYes(input.ReadLine());
        }

        // Step 3: Confirm and run
        output.WriteLine();
        output.WriteLine("Ready to consolidate with the following settings:");
        output.WriteLine($"  Directory:  {directory}");
        output.WriteLine($"  Dry run:    {(dryRun ? "Yes" : "No")}");
        output.WriteLine($"  Verbose:    {(verbose ? "Yes" : "No")}");
        output.WriteLine($"  Validate:   {(validate ? "Yes" : "No")}");
        output.WriteLine();
        output.Write("Proceed? [Y/n]: ");

        if (IsNo(input.ReadLine()))
        {
            output.WriteLine("Cancelled.");
            return 0;
        }

        output.WriteLine();

        // Execute
        if (validate)
        {
            return await RunWithValidation(directory, verbose, output, error);
        }

        RunConsolidation(directory, dryRun, verbose, output);
        return 0;
    }

    /// <summary>
    /// Searches for migration directories starting from the current directory.
    /// Returns candidate directories that contain .Designer.cs files.
    /// </summary>
    public static IReadOnlyList<string> SearchForMigrationDirectories(string baseDirectory)
    {
        var candidates = new List<string>();

        // Check well-known migration paths relative to base
        foreach (var relative in CommonMigrationPaths)
        {
            var candidate = Path.Combine(baseDirectory, relative);
            if (Directory.Exists(candidate) && ContainsDesignerFiles(candidate))
            {
                candidates.Add(candidate);
            }
        }

        // Search for directories containing .Designer.cs files (up to 4 levels deep)
        try
        {
            var designerFiles = Directory.GetFiles(baseDirectory, "*.Designer.cs", SearchOption.AllDirectories);
            foreach (var file in designerFiles)
            {
                var dir = Path.GetDirectoryName(file)!;
                if (!candidates.Contains(dir))
                {
                    candidates.Add(dir);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Ignore directories we can't access
        }

        return candidates;
    }

    private static async Task<string?> FindMigrationDirectory(TextReader input, TextWriter output, TextWriter error)
    {
        var baseDir = Directory.GetCurrentDirectory();
        output.WriteLine("Searching for EF Core migration files...");

        var candidates = SearchForMigrationDirectories(baseDir);

        if (candidates.Count == 0)
        {
            output.WriteLine("No migration directories found automatically.");
            output.WriteLine();
            output.Write("Enter the path to your migrations directory: ");
            var manualPath = input.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(manualPath))
            {
                error.WriteLine("No path provided.");
                return null;
            }

            var fullPath = Path.GetFullPath(manualPath);
            if (!Directory.Exists(fullPath))
            {
                error.WriteLine($"Directory not found: {fullPath}");
                return null;
            }

            return fullPath;
        }

        if (candidates.Count == 1)
        {
            output.WriteLine($"Found migration directory: {candidates[0]}");
            output.Write("Use this directory? [Y/n]: ");

            if (IsNo(input.ReadLine()))
            {
                output.Write("Enter the path to your migrations directory: ");
                var alt = input.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(alt))
                {
                    error.WriteLine("No path provided.");
                    return null;
                }

                var fullPath = Path.GetFullPath(alt);
                if (!Directory.Exists(fullPath))
                {
                    error.WriteLine($"Directory not found: {fullPath}");
                    return null;
                }
                return fullPath;
            }

            return candidates[0];
        }

        // Multiple candidates found
        output.WriteLine($"Found {candidates.Count} directories with migration files:");
        output.WriteLine();
        for (int i = 0; i < candidates.Count; i++)
        {
            var relPath = Path.GetRelativePath(baseDir, candidates[i]);
            var pairCount = MigrationDiscovery.FindMigrationPairs(candidates[i]).Count;
            output.WriteLine($"  [{i + 1}] {relPath} ({pairCount} migration pair{(pairCount == 1 ? "" : "s")})");
        }
        output.WriteLine($"  [{candidates.Count + 1}] Enter a different path");
        output.WriteLine();
        output.Write("Choose a directory: ");

        var selection = input.ReadLine()?.Trim();
        if (!int.TryParse(selection, out var index) || index < 1 || index > candidates.Count + 1)
        {
            error.WriteLine("Invalid selection.");
            return null;
        }

        if (index == candidates.Count + 1)
        {
            output.Write("Enter the path to your migrations directory: ");
            var custom = input.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(custom))
            {
                error.WriteLine("No path provided.");
                return null;
            }

            var fullPath = Path.GetFullPath(custom);
            if (!Directory.Exists(fullPath))
            {
                error.WriteLine($"Directory not found: {fullPath}");
                return null;
            }
            return fullPath;
        }

        return candidates[index - 1];
    }

    private static bool ContainsDesignerFiles(string directory)
    {
        try
        {
            return Directory.GetFiles(directory, "*.Designer.cs", SearchOption.AllDirectories).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsYes(string? response)
    {
        return response?.Trim().Equals("y", StringComparison.OrdinalIgnoreCase) == true
            || response?.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsNo(string? response)
    {
        return response?.Trim().Equals("n", StringComparison.OrdinalIgnoreCase) == true
            || response?.Trim().Equals("no", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static void RunConsolidation(string directory, bool dryRun, bool verbose, TextWriter output)
    {
        output.WriteLine(dryRun
            ? $"[DRY RUN] Scanning for migration pairs in: {directory}"
            : $"Consolidating migrations in: {directory}");

        var results = Consolidator.Run(directory, dryRun);

        if (results.Count == 0)
        {
            output.WriteLine("No migration pairs found.");
            return;
        }

        int consolidated = 0;
        int skipped = 0;

        foreach (var result in results)
        {
            var mainFileName = Path.GetFileName(result.MainFilePath);

            if (result.WasSkipped)
            {
                skipped++;
                if (verbose)
                {
                    output.WriteLine($"  SKIP  {mainFileName}: {result.SkipReason}");
                }
            }
            else
            {
                consolidated++;
                var prefix = dryRun ? "  WOULD" : "  OK   ";
                output.WriteLine($"{prefix} {mainFileName}");
                if (verbose && result.ArchivedPath is not null)
                {
                    output.WriteLine($"         Archived: {Path.GetFileName(result.ArchivedPath)}");
                }
            }
        }

        output.WriteLine();
        output.WriteLine(dryRun
            ? $"Would consolidate {consolidated} migration(s), skip {skipped}."
            : $"Consolidated {consolidated} migration(s), skipped {skipped}.");
    }

    private static async Task<int> RunWithValidation(string directory, bool verbose, TextWriter output, TextWriter error)
    {
        output.WriteLine($"Validating consolidation in: {directory}");

        output.WriteLine("Step 1/3: Generating migration SQL (before)...");
        var (beforeValid, beforeSql, _, beforeError) = await Validator.ValidateAsync(directory);

        if (!beforeValid)
        {
            error.WriteLine($"Error generating SQL before consolidation: {beforeError}");
            error.WriteLine("Note: --validate requires 'dotnet ef' tools to be installed and the project to be buildable.");
            return 1;
        }

        output.WriteLine("Step 2/3: Consolidating migrations...");
        var results = Consolidator.Run(directory, dryRun: false);

        int consolidated = results.Count(r => !r.WasSkipped);
        output.WriteLine($"  Consolidated {consolidated} migration(s).");

        output.WriteLine("Step 3/3: Generating migration SQL (after)...");
        var (afterValid, afterSql, _, afterError) = await Validator.ValidateAsync(directory);

        if (!afterValid)
        {
            error.WriteLine($"Error generating SQL after consolidation: {afterError}");
            return 1;
        }

        var (isValid, diff) = Validator.CompareSql(beforeSql!, afterSql!);

        if (isValid)
        {
            output.WriteLine();
            output.WriteLine("✓ Validation passed: SQL output is identical before and after consolidation.");
            return 0;
        }

        error.WriteLine();
        error.WriteLine("✗ Validation FAILED: SQL output differs.");
        if (verbose && diff is not null)
        {
            error.WriteLine(diff);
        }
        return 1;
    }
}
