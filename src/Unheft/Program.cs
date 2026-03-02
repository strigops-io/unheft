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

var projectOption = new Option<string?>("--project", "-p")
{
    Description = "Path to the target project (.csproj) containing the migrations"
};

var startupProjectOption = new Option<string?>("--startup-project", "-s")
{
    Description = "Path to the startup project (.csproj) used as the runtime host"
};

var contextOption = new Option<string?>("--context")
{
    Description = "The DbContext class name to use"
};

var rootCommand = new RootCommand("unheft — EF Core Migration Designer File Consolidation Tool");
rootCommand.Add(pathArgument);
rootCommand.Add(dryRunOption);
rootCommand.Add(validateOption);
rootCommand.Add(verboseOption);
rootCommand.Add(projectOption);
rootCommand.Add(startupProjectOption);
rootCommand.Add(contextOption);

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var path = parseResult.GetValue(pathArgument)!;
    var dryRun = parseResult.GetValue(dryRunOption);
    var validate = parseResult.GetValue(validateOption);
    var verbose = parseResult.GetValue(verboseOption);
    var project = parseResult.GetValue(projectOption);
    var startupProject = parseResult.GetValue(startupProjectOption);
    var context = parseResult.GetValue(contextOption);

    var directory = path.FullName;

    if (!Directory.Exists(directory))
    {
        Console.Error.WriteLine($"Error: Directory not found: {directory}");
        Environment.ExitCode = 1;
        return;
    }

    if (validate)
    {
        // Auto-detect project if not provided
        project ??= ProjectDetector.FindProjectFile(directory);

        if (project is null)
        {
            Console.Error.WriteLine("Error: Could not detect the project file (.csproj). Use --project (-p) to specify it.");
            Environment.ExitCode = 1;
            return;
        }

        // Auto-detect startup project if not provided and project is a class library
        if (startupProject is null && ProjectDetector.IsClassLibrary(project))
        {
            var candidates = ProjectDetector.FindStartupProjects(project);
            if (candidates.Count == 1)
            {
                startupProject = candidates[0];
                Console.WriteLine($"Detected startup project: {startupProject}");
            }
            else if (candidates.Count > 1)
            {
                Console.Error.WriteLine("Error: Multiple startup project candidates found. Use --startup-project (-s) to specify one:");
                foreach (var c in candidates)
                    Console.Error.WriteLine($"  {c}");
                Environment.ExitCode = 1;
                return;
            }
            else
            {
                Console.Error.WriteLine("Error: The target project is a class library and no startup project was found.");
                Console.Error.WriteLine("Use --startup-project (-s) to specify a runnable project.");
                Environment.ExitCode = 1;
                return;
            }
        }

        await RunWithValidation(directory, verbose, project, startupProject, context);
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

static async Task RunWithValidation(string directory, bool verbose, string? project, string? startupProject, string? context)
{
    Console.WriteLine($"Validating consolidation in: {directory}");

    // Check for pending model changes
    if (project is not null)
    {
        Console.WriteLine("Checking for pending model changes...");
        var (hasPending, pendingError) = await Validator.CheckPendingModelChangesAsync(
            project, startupProject, context);

        if (hasPending)
        {
            Console.Error.WriteLine("Warning: There are pending model changes that have not been added as a migration.");
            if (verbose && pendingError is not null)
                Console.Error.WriteLine(pendingError);
            Console.Error.WriteLine("Consider running 'dotnet ef migrations add' before validating.");
        }
    }

    // Step 1: Generate SQL before consolidation
    Console.WriteLine("Step 1/3: Generating migration SQL (before)...");
    var (beforeValid, beforeSql, _, beforeError) = await Validator.ValidateAsync(
        project ?? directory, startupProject, context);

    if (!beforeValid)
    {
        Console.Error.WriteLine($"Error generating SQL before consolidation: {beforeError}");
        Console.Error.WriteLine("Note: --validate requires 'dotnet ef' tools to be installed and the project to be buildable.");
        if (startupProject is null && project is not null && ProjectDetector.IsClassLibrary(project))
        {
            Console.Error.WriteLine("Hint: The target project appears to be a class library. Use --startup-project (-s) to specify a runnable project.");
        }
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
    var (afterValid, afterSql, _, afterError) = await Validator.ValidateAsync(
        project ?? directory, startupProject, context);

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
