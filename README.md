# unheft

**EF Core Migration Designer File Consolidation Tool**

unheft consolidates split EF Core migration files (`.cs` + `.Designer.cs`) into single files. It extracts the `[DbContext]` and `[Migration]` attributes from Designer files and adds them to the main migration file, then archives the Designer file—improving code organization and reducing clutter.

## Installation

```bash
dotnet tool install -g unheft
```

## Quick Start

### Interactive Wizard

Run `unheft` with no arguments to launch the interactive wizard:

```bash
unheft
```

The wizard will:
1. Ask whether you'd like help or to use the guided wizard
2. Search your project for directories containing EF Core migration files
3. Let you pick the correct migration directory (or enter a path manually)
4. Ask about options (dry run, verbose, validation)
5. Run the consolidation

### Direct Usage

Point unheft at your migrations directory:

```bash
unheft path/to/Migrations
```

## Examples

### Preview changes without modifying files

```bash
unheft path/to/Migrations --dry-run
```

Example output:

```
[DRY RUN] Scanning for migration pairs in: path/to/Migrations
  WOULD 20251215053906_MgRemoveCustomerEmailJourney.cs
  WOULD 20251216120000_MgAddOrderTable.cs

Would consolidate 2 migration(s), skip 0.
```

### Consolidate migrations in the current directory

```bash
unheft .
```

### Consolidate with verbose output

```bash
unheft path/to/Migrations --verbose
```

Example output:

```
Consolidating migrations in: path/to/Migrations
  OK    20251215053906_MgRemoveCustomerEmailJourney.cs
         Archived: 20251215053906_MgRemoveCustomerEmailJourney.Designer.cs.archived
  SKIP  20251216120000_MgAddOrderTable.cs: Already consolidated (attributes present in main file)

Consolidated 1 migration(s), skipped 1.
```

### Validate consolidation is lossless

Use `--validate` to confirm the generated SQL is identical before and after consolidation. This requires `dotnet ef` tools to be installed and the project to be buildable.

```bash
unheft path/to/project --validate
```

Example output:

```
Validating consolidation in: path/to/project
Step 1/3: Generating migration SQL (before)...
Step 2/3: Consolidating migrations...
  Consolidated 3 migration(s).
Step 3/3: Generating migration SQL (after)...

✓ Validation passed: SQL output is identical before and after consolidation.
```

### Combine options

```bash
unheft path/to/Migrations --dry-run --verbose
```

## What It Does

For each migration pair found:

1. **Extracts** `[DbContext(typeof(...))]` and `[Migration("...")]` attributes from the `.Designer.cs` file
2. **Adds** those attributes to the main `.cs` migration file
3. **Adds** a `BuildTargetModel` stub to the main file (if not already present)
4. **Ensures** required `using` directives are present
5. **Archives** the Designer file by renaming it to `.Designer.cs.archived`

### Before

```
Migrations/
├── 20251215_AddCustomer.cs
├── 20251215_AddCustomer.Designer.cs    ← contains [DbContext] and [Migration]
├── 20251216_AddOrders.cs
└── 20251216_AddOrders.Designer.cs      ← contains [DbContext] and [Migration]
```

### After

```
Migrations/
├── 20251215_AddCustomer.cs              ← now contains [DbContext] and [Migration]
├── 20251215_AddCustomer.Designer.cs.archived
├── 20251216_AddOrders.cs                ← now contains [DbContext] and [Migration]
└── 20251216_AddOrders.Designer.cs.archived
```

## Options

| Option | Description |
|---|---|
| `path` | Directory containing EF Core migrations (defaults to current directory) |
| `--dry-run` | Preview changes without modifying any files |
| `--validate` | Validate that consolidation is semantically lossless by comparing generated SQL |
| `--verbose` | Show detailed output |
| `--help` | Show help information |

## Safety

- **Idempotent**: Running unheft multiple times is safe—already-consolidated migrations are skipped
- **Non-destructive**: Designer files are archived (renamed), not deleted
- **Validation**: Use `--validate` to prove the SQL output is unchanged
- **Dry run**: Preview all changes before committing with `--dry-run`

## Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build

```bash
dotnet build
```

### Run tests

```bash
dotnet test
```

### Run the tool locally (without installing globally)

Use `dotnet run` and pass arguments after `--`:

```bash
# Interactive wizard
dotnet run --project src/Unheft

# Point at a migrations directory
dotnet run --project src/Unheft -- path/to/Migrations

# Dry run
dotnet run --project src/Unheft -- path/to/Migrations --dry-run --verbose
```

### Debug in VS Code

Open the repository in VS Code with the C# Dev Kit extension. Create a `.vscode/launch.json` with the following configuration to run and debug the tool:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Run unheft",
      "type": "dotnet",
      "request": "launch",
      "projectPath": "${workspaceFolder}/src/Unheft/Unheft.csproj",
      "args": ["path/to/Migrations", "--dry-run", "--verbose"]
    }
  ]
}
```

Adjust the `args` array to match the invocation you want to step through, then press **F5** to start debugging.

### Install from a local build

Use the provided script to pack the project and install it as a global tool in one step:

```bash
scripts/link-local
```

This script:
1. Packs `src/Unheft` into `artifacts/nupkg/`
2. Uninstalls any existing global `unheft` installation
3. Installs the freshly built package globally

After running it, `unheft` in your shell resolves to the local build. Re-run the script whenever you want to pick up new changes.

To uninstall:

```bash
dotnet tool uninstall -g unheft
```