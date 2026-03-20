`third_party/tokei` stores platform sidecar binaries discovered by `ProcessTokeiRunner`.

Current layout:
- `win-x64/tokei.exe` — built from crate `tokei` `12.1.2`
- `osx-arm64/tokei` — reserved path for the secondary publish target

The application resolves sidecars relative to `AppContext.BaseDirectory` and falls back to `PATH` when the platform binary is absent.
