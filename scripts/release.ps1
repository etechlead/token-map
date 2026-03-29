param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$')]
    [string]$NextVersion = "",
    [string]$ReleaseTitle = "",
    [string]$Notes = "",
    [string]$NotesFile = "",
    [switch]$SkipWindowsPackagingCheck,
    [switch]$SkipNextVersionBump,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "packaging-metadata.ps1")

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @(),
        [switch]$Mutating
    )

    $display = if ($ArgumentList.Count -gt 0) { "$FilePath $($ArgumentList -join ' ')" } else { $FilePath }
    Write-Host ">> $display"

    if ($DryRun -and $Mutating) {
        return
    }

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $display"
    }
}

function Get-CommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    $output = & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        $display = if ($ArgumentList.Count -gt 0) { "$FilePath $($ArgumentList -join ' ')" } else { $FilePath }
        throw "Command failed: $display"
    }

    return ($output -join "`n").Trim()
}

function Assert-ToolAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ToolName
    )

    if ($null -eq (Get-Command $ToolName -ErrorAction SilentlyContinue)) {
        throw "Required tool '$ToolName' was not found."
    }
}

function Assert-CleanWorkingTree {
    $status = Get-CommandOutput -FilePath "git" -ArgumentList @("status", "--short")
    if (-not [string]::IsNullOrWhiteSpace($status)) {
        throw "Working tree is not clean. Commit or stash changes before running release.ps1."
    }
}

function Assert-MainBranch {
    $currentBranch = Get-CommandOutput -FilePath "git" -ArgumentList @("branch", "--show-current")
    if ($currentBranch -ne "main") {
        throw "Releases must be cut from 'main'. Current branch: '$currentBranch'."
    }
}

function Get-DefaultNextLocalVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    [version]$parsedVersion = $ReleaseVersion
    return "{0}.{1}.{2}-local" -f $parsedVersion.Major, $parsedVersion.Minor, ($parsedVersion.Build + 1)
}

function Assert-TagDoesNotExist {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $localTag = Get-CommandOutput -FilePath "git" -ArgumentList @("tag", "--list", $TagName)
    if (-not [string]::IsNullOrWhiteSpace($localTag)) {
        throw "Git tag '$TagName' already exists locally."
    }

    $remoteTag = Get-CommandOutput -FilePath "git" -ArgumentList @("ls-remote", "--tags", "origin", "refs/tags/$TagName")
    if (-not [string]::IsNullOrWhiteSpace($remoteTag)) {
        throw "Git tag '$TagName' already exists on origin."
    }
}

function Resolve-NotesFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$InlineNotes,
        [Parameter(Mandatory = $true)]
        [string]$RequestedNotesFile
    )

    if (-not [string]::IsNullOrWhiteSpace($InlineNotes) -and -not [string]::IsNullOrWhiteSpace($RequestedNotesFile)) {
        throw "Use either -Notes or -NotesFile, not both."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedNotesFile)) {
        if ([System.IO.Path]::IsPathRooted($RequestedNotesFile)) {
            return $RequestedNotesFile
        }

        return Join-Path $RepoRoot $RequestedNotesFile
    }

    if ([string]::IsNullOrWhiteSpace($InlineNotes)) {
        throw "Release notes are required. Generate them with the agent and pass -Notes or -NotesFile."
    }

    $tempFile = Join-Path ([System.IO.Path]::GetTempPath()) "tokenmap-release-notes-$Version.md"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($tempFile, $InlineNotes, $utf8NoBom)
    return $tempFile
}

function Invoke-SetVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetVersion
    )

    Invoke-ExternalCommand -FilePath "powershell" -ArgumentList @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $PSScriptRoot "set-version.ps1"),
        "-Version", $TargetVersion
    ) -Mutating
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFilePath = Get-VersionMetadataPath -RepoRoot $repoRoot
$versionGitPath = Split-Path -Path $versionFilePath -Leaf
$currentVersion = Get-RepoVersion -RepoRoot $repoRoot
$releaseTag = "v$Version"
$releaseTitleValue = if ([string]::IsNullOrWhiteSpace($ReleaseTitle)) { "TokenMap $Version" } else { $ReleaseTitle }

if ([string]::IsNullOrWhiteSpace($NextVersion) -and -not $SkipNextVersionBump) {
    $NextVersion = Get-DefaultNextLocalVersion -ReleaseVersion $Version
}

if (-not $SkipNextVersionBump -and $NextVersion -eq $Version) {
    throw "Next version must differ from release version."
}

if ($currentVersion -eq $Version) {
    throw "Repo version is already '$Version'. Run release.ps1 from the next local version state."
}

Assert-ToolAvailable -ToolName "git"
Assert-ToolAvailable -ToolName "gh"
Assert-ToolAvailable -ToolName "dotnet"

if (-not $SkipWindowsPackagingCheck) {
    Assert-ToolAvailable -ToolName "powershell"
}

Assert-CleanWorkingTree
Assert-MainBranch
Assert-TagDoesNotExist -TagName $releaseTag

$notesFilePath = Resolve-NotesFilePath -RepoRoot $repoRoot -InlineNotes $Notes -RequestedNotesFile $NotesFile
$createdTemporaryNotesFile = [string]::IsNullOrWhiteSpace($NotesFile)

if (-not (Test-Path $notesFilePath -PathType Leaf)) {
    throw "Release notes file was not found at '$notesFilePath'."
}

try {
    Invoke-SetVersion -TargetVersion $Version

    Invoke-ExternalCommand -FilePath "dotnet" -ArgumentList @("restore", "Clever.TokenMap.sln")
    Invoke-ExternalCommand -FilePath "dotnet" -ArgumentList @("build", "Clever.TokenMap.sln")
    Invoke-ExternalCommand -FilePath "dotnet" -ArgumentList @("test", "Clever.TokenMap.sln", "--no-build")

    if (-not $SkipWindowsPackagingCheck) {
        Invoke-ExternalCommand -FilePath "powershell" -ArgumentList @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", (Join-Path $PSScriptRoot "package-windows.ps1"),
            "-Version", $Version
        )
    }

    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("add", "--", $versionGitPath) -Mutating
    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("commit", "-m", "chore(release): prepare $Version") -Mutating
    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("push", "origin", "main") -Mutating
    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("tag", "-a", $releaseTag, "-m", $releaseTitleValue) -Mutating
    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("push", "origin", $releaseTag) -Mutating
    Invoke-ExternalCommand -FilePath "gh" -ArgumentList @(
        "release", "create", $releaseTag,
        "--repo", "etechlead/token-map",
        "--title", $releaseTitleValue,
        "--notes-file", $notesFilePath
    ) -Mutating

    if (-not $SkipNextVersionBump) {
        Invoke-SetVersion -TargetVersion $NextVersion
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("add", "--", $versionGitPath) -Mutating
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("commit", "-m", "chore(release): start $NextVersion") -Mutating
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("push", "origin", "main") -Mutating
    }
}
finally {
    if ($createdTemporaryNotesFile -and (Test-Path $notesFilePath -PathType Leaf)) {
        Remove-Item -Path $notesFilePath -Force -ErrorAction SilentlyContinue
    }
}
