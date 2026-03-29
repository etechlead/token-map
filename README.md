# TokenMap

TokenMap is a desktop app for exploring where a codebase gets heavy.
Open a local folder and inspect the same project as both a tree and a treemap, measured by tokens, non-empty lines, or file size.

<img src="docs/readme/screenshot.png" alt="TokenMap main window" width="838">

## Why TokenMap?

- See which folders and files actually dominate a repository instead of guessing from file count alone.
- Compare token-heavy areas before feeding code to LLM workflows.
- Spot hotspots before refactors, cleanup, or architecture discussions.

## How It Works

- TokenMap scans a local folder into one snapshot.
- `.gitignore`, global excludes, and folder excludes decide what gets in.
- Only included files are measured and shown.
- Text files get local token counts, non-empty line counts, and byte size.
- Folder weight is aggregated from descendants.

> [!NOTE]
> Token counts are computed locally with the `o200k_base` tokenizer via `Microsoft.ML.Tokenizers`.
> They are useful for comparing files and folders inside the same repository, but should not be treated as exact billing or context-window numbers for every model.

## Install

- Windows: download the latest `TokenMap-win-x64-<version>-installer.exe` from GitHub Releases, run it, and follow the installer steps. The installer is per-user and does not require administrator rights.
- macOS: download the latest `TokenMap-macos-arm64-<version>.dmg` from GitHub Releases, open it, and drag `TokenMap.app` into `Applications`.
- Linux: release packaging is planned but not available yet.

<details>
<summary>macOS: first launch for the unsigned build</summary>

TokenMap is currently distributed as an unsigned app, so macOS may block the first launch.

UI path:

1. Move `TokenMap.app` to `Applications`.
2. Try to open it once, then dismiss the warning.
3. Open `System Settings > Privacy & Security`.
4. Find the message about `TokenMap` being blocked and click `Open Anyway`.
5. Confirm the follow-up prompt and launch the app again.

Terminal alternative:

```bash
xattr -dr com.apple.quarantine /Applications/TokenMap.app
```

Then launch `TokenMap.app` again.
</details>

## Build From Source

Prerequisite: .NET SDK `10.0.201` or newer in the same feature band.

```powershell
dotnet restore Clever.TokenMap.sln
dotnet build Clever.TokenMap.sln
dotnet run --project .\src\Clever.TokenMap.App\Clever.TokenMap.App.csproj
```

Run tests:

```powershell
dotnet test Clever.TokenMap.sln --no-build
```

For repo conventions and architecture details, see `docs/architecture.md` and `docs/workflow.md`.

## Tech Stack

- .NET 10
- C#
- Avalonia
- CommunityToolkit.Mvvm
- Microsoft.ML.Tokenizers

## License

TokenMap is available under the [MIT License](LICENSE).
