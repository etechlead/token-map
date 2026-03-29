# TokenMap - Logging And Runtime Issues

## Canonical Models

- `AppLogEntry` is the canonical structured log payload.
- `AppIssue` is the canonical runtime issue model for shell actions and global exception handling.
- `AnalysisIssue` is the canonical snapshot diagnostic model for scanner and metrics-enrichment problems that do not stop analysis.

## Logging Flow

- Runtime code logs through `IAppLogger` and `IAppLoggerFactory`.
- File-backed logging lives in infrastructure through `AppLoggerFactory`.
- User-facing runtime failures write a structured log entry with a reference ID that can be shown in the shell.
- Settings persistence logs load/save failures locally and falls back to defaults or best-effort persistence behavior; settings failures do not raise shell-level user-facing issues.

## User-Facing Runtime Issues

- The shell renders non-fatal issues as a banner and fatal issues as a modal.

## Global Error Boundaries

- Startup failure is logged before the app exits.
- Dispatcher, background task, and AppDomain boundary failures are logged through the same runtime issue path when the process can still surface UI.
- On non-Windows startup failure paths, the app falls back to stderr output instead of assuming a native Windows dialog is available.
