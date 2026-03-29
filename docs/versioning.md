# TokenMap - Versioning

## Scope

- This document is the canonical repo policy for application versioning, Git tags, GitHub Releases, and release artifact naming.
- Keep versioning rules here rather than duplicating them across workflow notes, packaging scripts, or release descriptions.

## Release Identity

- TokenMap uses Semantic Versioning in the form `MAJOR.MINOR.PATCH`.
- Until `1.0.0`, `MINOR` may include user-visible behavior changes that would otherwise count as breaking changes; `PATCH` stays reserved for fixes and low-risk polish.
- Official release tags use the same public version with a `v` prefix, for example `v0.1.0`.
- Public prereleases use SemVer prerelease labels on the same tag model, for example `v0.1.1-beta.1` or `v0.1.1-rc.1`.

## Branching Model

- `main` is the only long-lived development branch.
- Short-lived topic branches may be used for features, fixes, or chores, then merged back to `main`.
- Official releases are cut from tagged commits on `main`.
- Do not introduce a permanent `develop` branch or full GitFlow unless the repo's release pressure changes enough to justify that complexity.

## Build Identity

- Every build should have a user-facing version string and a diagnostic build identity.
- Official release builds use the release version directly, for example `0.1.0`.
- Non-release builds may append a prerelease or local suffix such as `0.1.1-dev.15` or `0.1.1-local`.
- Non-release builds may include commit metadata for diagnostics, for example a short SHA and a dirty-working-tree marker.
- Local builds must keep working even when git metadata is unavailable; they must fall back to a safe local version instead of failing the build.

## App And GitHub Alignment

- The version shown in the application, the Git tag, the GitHub Release title, and packaged artifact names should all derive from the same public version.
- GitHub Release titles should use the product name plus the public version, for example `TokenMap 0.1.0`.
- Packaged artifact names should include platform, architecture, version, and only the qualifier needed to distinguish user-visible sibling deliverables, for example `TokenMap-windows-x64-0.1.0-setup.exe`.
- Use `-portable` for archive formats where the filename must tell the user that the download is the no-install variant, for example `TokenMap-windows-x64-0.1.0-portable.zip` or `TokenMap-linux-x64-0.1.0-portable.tar.gz`.
- Do not repeat obvious installer concepts for formats that already imply installation, such as `.dmg` or `.deb`.
- When the distribution status is material to the artifact, append a clear qualifier such as `-unsigned`, for example `TokenMap-macos-arm64-0.1.0-portable-unsigned.zip`.
- The app UI should show a concise user-facing version and may expose the diagnostic build identity separately.

## Operational Flow

- Keep one next planned public version in build metadata for ongoing development.
- Build and test `main` continuously with non-release version strings.
- Create an official release by tagging the intended commit on `main` with the matching `vX.Y.Z` version.
- Publish GitHub release assets from that tagged commit.
- After an official release, bump the next planned version in build metadata rather than continuing to build new work as the old release number.
- Default the next planned version to the next patch release, for example `0.1.0` -> `0.1.1-local`, unless there is an explicit decision to start the next minor or major line immediately.
