using System.CommandLine;
using Unheft.Services;

// When no arguments are provided, launch the interactive wizard
if (args.Length == 0 && !Console.IsInputRedirected)
{
    var result = await Wizard.RunAsync(Console.In, Console.Out, Console.Error);
    if (result == -1)
    {
        // User chose help — fall through to System.CommandLine with --help
        args = ["--help"];
    }
    else
    {
        return result;
    }
}

var pathArgument = new Argument<DirectoryInfo>("path")
{
    Description = "Path to the directory containing EF Core migrations",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory())
};

var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "Preview changes without modifying any files"
};

var validateOption = new Option<bool>("--validate")
{
    Description = "Validate that consolidation is semantically lossless by comparing generated SQL before and after"
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Show detailed output"
};

var rootCommand = new RootCommand("unheft — EF Core Migration Designer File Consolidation Tool");
rootCommand.Add(pathArgument);
rootCommand.Add(dryRunOption);
rootCommand.Add(validateOption);
rootCommand.Add(verboseOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var dryRun = parseResult.GetValue(dryRunOption);
    var validate = parseResult.GetValue(validateOption);
    var verbose = parseResult.GetValue(verboseOption);

    var directory = path.FullName;

    if (!Directory.Exists(directory))
    {
        Console.Error.WriteLine($"Error: Directory not found: {directory}");
        Environment.ExitCode = 1;
        return;
    }

    if (validate)
    {
        await RunWithValidation(directory, verbose);
    }
    else
    {
        RunConsolidation(directory, dryRun, verbose);
    }
});

var config = new CommandLineConfiguration(rootCommand);
return await config.InvokeAsync(args);

static void RunConsolidation(string directory, bool dryRun, bool verbose)
{
    Console.WriteLine(dryRun
        ? $"[DRY RUN] Scanning for migration pairs in: {directory}"
        : $"Consolidating migrations in: {directory}");

    var results = Consolidator.Run(directory, dryRun);

    if (results.Count == 0)
    {
        Console.WriteLine("No migration pairs found.");
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
                Console.WriteLine($"  SKIP  {mainFileName}: {result.SkipReason}");
            }
        }
        else
        {
            consolidated++;
            var prefix = dryRun ? "  WOULD" : "  OK   ";
            Console.WriteLine($"{prefix} {mainFileName}");
            if (verbose && result.ArchivedPath is not null)
            {
                Console.WriteLine($"         Archived: {Path.GetFileName(result.ArchivedPath)}");
            }
        }
    }

    Console.WriteLine();
    Console.WriteLine(dryRun
        ? $"Would consolidate {consolidated} migration(s), skip {skipped}."
        : $"Consolidated {consolidated} migration(s), skipped {skipped}.");
}

static async Task RunWithValidation(string directory, bool verbose)
{
    Console.WriteLine($"Validating consolidation in: {directory}");

    // Step 1: Generate SQL before consolidation
    Console.WriteLine("Step 1/3: Generating migration SQL (before)...");
    var (beforeValid, beforeSql, _, beforeError) = await Validator.ValidateAsync(directory);

    if (!beforeValid)
    {
        Console.Error.WriteLine($"Error generating SQL before consolidation: {beforeError}");
        Console.Error.WriteLine("Note: --validate requires 'dotnet ef' tools to be installed and the project to be buildable.");
        Environment.ExitCode = 1;
        return;
    }

    // Step 2: Run consolidation
    Console.WriteLine("Step 2/3: Consolidating migrations...");
    var results = Consolidator.Run(directory, dryRun: false);

    int consolidated = results.Count(r => !r.WasSkipped);
    Console.WriteLine($"  Consolidated {consolidated} migration(s).");

    // Step 3: Generate SQL after consolidation
    Console.WriteLine("Step 3/3: Generating migration SQL (after)...");
    var (afterValid, afterSql, _, afterError) = await Validator.ValidateAsync(directory);

    if (!afterValid)
    {
        Console.Error.WriteLine($"Error generating SQL after consolidation: {afterError}");
        Environment.ExitCode = 1;
        return;
    }

    // Compare
    var (isValid, diff) = Validator.CompareSql(beforeSql!, afterSql!);

    if (isValid)
    {
        Console.WriteLine();
        Console.WriteLine("✓ Validation passed: SQL output is identical before and after consolidation.");
    }
    else
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine("✗ Validation FAILED: SQL output differs.");
        if (verbose && diff is not null)
        {
            Console.Error.WriteLine(diff);
        }
        Environment.ExitCode = 1;
    }
}
