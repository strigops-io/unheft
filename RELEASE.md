# Release Process

Strigops Unheft uses a two-step release workflow powered by GitHub Actions.

## How It Works

### 1. Prepare Release (automatic)

Every push to `main` triggers the **Prepare Release** workflow (`.github/workflows/prepare.yml`). It:

1. Checks out the code and sets up .NET
2. Determines the package version automatically (`<Major>.<Minor>.<commit-count>`)
3. Builds, tests, and packs the NuGet package
4. Uploads the `.nupkg` as a GitHub Actions artifact (retained for 90 days)

The base `Major.Minor` version is read from `src/Unheft/Unheft.csproj` (`<Version>`). The patch number is the total commit count on the branch, so each commit to `main` produces a unique, monotonically increasing version.

Pull requests also run the build and test steps but do **not** upload an artifact.

### 2. Release to NuGet (manual)

When you are ready to publish, trigger the **Release to NuGet** workflow (`.github/workflows/release.yml`) manually from the GitHub Actions UI:

1. Go to **Actions → Release to NuGet → Run workflow**
2. Optionally enter a specific *Prepare Release* run ID; leave blank to use the latest successful run
3. Click **Run workflow**

The workflow downloads the artifact from the prepare step and pushes it to [NuGet.org](https://www.nuget.org/packages/unheft) using the `NUGET_API_KEY` secret.

## Setup

### Repository secret

Add a NuGet.org API key as a repository secret:

1. Go to **Settings → Secrets and variables → Actions**
2. Create a new secret named `NUGET_API_KEY` with your NuGet.org API key
3. (Recommended) Scope the key to the `unheft` package with push-only permissions

### Environment protection (recommended)

The release workflow uses a `nuget` environment. To require approval before publishing:

1. Go to **Settings → Environments → New environment** → name it `nuget`
2. Enable **Required reviewers** and add one or more approvers

## Bumping the Version

To bump the major or minor version, update the `<Version>` element in `src/Unheft/Unheft.csproj`:

```xml
<Version>2.0.0</Version>
```

The patch number will continue to auto-increment from commit count.
