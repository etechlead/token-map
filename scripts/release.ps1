param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$')]
    [string]$NextVersion = "",
    [string]$ReleaseTitle = "",
    [string]$Notes = "",
    [string]$NotesFile = "",
    [switch]$WaitForReleaseAssets,
    [ValidateRange(1, 120)]
    [int]$ReleaseAssetsTimeoutMinutes = 25,
    [ValidateRange(5, 300)]
    [int]$ReleaseAssetsPollSeconds = 15,
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

function Get-JsonCommandOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    $output = Get-CommandOutput -FilePath $FilePath -ArgumentList $ArgumentList
    if ([string]::IsNullOrWhiteSpace($output)) {
        return $null
    }

    return $output | ConvertFrom-Json
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
        [string]$InlineNotes,
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

function Get-ExpectedReleaseAssetNames {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion
    )

    return @(
        "TokenMap-windows-x64-$ReleaseVersion-setup.exe",
        "TokenMap-windows-x64-$ReleaseVersion-portable.zip",
        "TokenMap-macos-arm64-$ReleaseVersion-unsigned.dmg",
        "TokenMap-macos-arm64-$ReleaseVersion-portable-unsigned.zip",
        "TokenMap-linux-x64-$ReleaseVersion.deb",
        "TokenMap-linux-x64-$ReleaseVersion-portable.tar.gz"
    )
}

function Get-ReleaseWorkflowState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$WorkflowFile,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseCommitSha
    )

    $runs = @(Get-JsonCommandOutput -FilePath "gh" -ArgumentList @(
        "run", "list",
        "--repo", $Repo,
        "--workflow", $WorkflowFile,
        "--event", "release",
        "--limit", "20",
        "--json", "databaseId,workflowName,status,conclusion,url,headSha,createdAt,displayTitle"
    ))

    $matchingRun = $runs |
        Where-Object { $_.headSha -eq $ReleaseCommitSha } |
        Sort-Object createdAt -Descending |
        Select-Object -First 1

    if ($null -eq $matchingRun) {
        return [pscustomobject]@{
            WorkflowFile = $WorkflowFile
            WorkflowName = [System.IO.Path]::GetFileNameWithoutExtension($WorkflowFile)
            DatabaseId = $null
            Status = "not_found"
            Conclusion = ""
            Url = ""
        }
    }

    return [pscustomobject]@{
        WorkflowFile = $WorkflowFile
        WorkflowName = $matchingRun.workflowName
        DatabaseId = $matchingRun.databaseId
        Status = $matchingRun.status
        Conclusion = $matchingRun.conclusion
        Url = $matchingRun.url
    }
}

function Get-ReleaseAssetState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedAssetNames
    )

    $release = Get-JsonCommandOutput -FilePath "gh" -ArgumentList @(
        "release", "view", $ReleaseTag,
        "--repo", $Repo,
        "--json", "url,assets"
    )

    $assets = @($release.assets)
    $uploadedAssetNames = @(
        $assets |
            Where-Object { $_.state -eq "uploaded" } |
            ForEach-Object { $_.name }
    )

    $missingAssetNames = @(
        $ExpectedAssetNames |
            Where-Object { $uploadedAssetNames -notcontains $_ }
    )

    return [pscustomobject]@{
        ReleaseUrl = $release.url
        UploadedAssetNames = $uploadedAssetNames
        MissingAssetNames = $missingAssetNames
    }
}

function Wait-ForReleaseAssets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repo,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseTag,
        [Parameter(Mandatory = $true)]
        [string]$ReleaseCommitSha,
        [Parameter(Mandatory = $true)]
        [string[]]$WorkflowFiles,
        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedAssetNames,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutMinutes,
        [Parameter(Mandatory = $true)]
        [int]$PollSeconds
    )

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)

    while ((Get-Date) -lt $deadline) {
        $workflowStates = foreach ($workflowFile in $WorkflowFiles) {
            Get-ReleaseWorkflowState -Repo $Repo -WorkflowFile $workflowFile -ReleaseCommitSha $ReleaseCommitSha
        }

        foreach ($workflowState in $workflowStates) {
            $idText = if ($null -ne $workflowState.DatabaseId) { " [$($workflowState.DatabaseId)]" } else { "" }
            Write-Host ("{0}{1}: {2} / {3}" -f $workflowState.WorkflowName, $idText, $workflowState.Status, $workflowState.Conclusion)
        }

        $failedWorkflows = @(
            $workflowStates |
                Where-Object {
                    $_.Status -eq "completed" -and
                    $_.Conclusion -ne "success"
                }
        )
        if ($failedWorkflows.Count -gt 0) {
            $failedNames = $failedWorkflows | ForEach-Object { $_.WorkflowName }
            throw "Release workflows failed for '$ReleaseTag': $($failedNames -join ', ')"
        }

        $allWorkflowsCompleted = @(
            $workflowStates |
                Where-Object { $_.Status -eq "completed" }
        ).Count -eq $WorkflowFiles.Count

        if ($allWorkflowsCompleted) {
            $assetState = Get-ReleaseAssetState -Repo $Repo -ReleaseTag $ReleaseTag -ExpectedAssetNames $ExpectedAssetNames
            if ($assetState.MissingAssetNames.Count -eq 0) {
                Write-Host "Verified release assets on $($assetState.ReleaseUrl):"
                foreach ($assetName in $ExpectedAssetNames) {
                    Write-Host "  - $assetName"
                }

                return
            }

            Write-Host "Release '$ReleaseTag' is still missing assets:"
            foreach ($assetName in $assetState.MissingAssetNames) {
                Write-Host "  - $assetName"
            }
        }

        Start-Sleep -Seconds $PollSeconds
    }

    throw "Timed out waiting for release workflows and assets for '$ReleaseTag'."
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
$githubRepo = "etechlead/token-map"
$versionFilePath = Get-VersionMetadataPath -RepoRoot $repoRoot
$versionGitPath = Split-Path -Path $versionFilePath -Leaf
$currentVersion = Get-RepoVersion -RepoRoot $repoRoot
$releaseTag = "v$Version"
$releaseTitleValue = if ([string]::IsNullOrWhiteSpace($ReleaseTitle)) { "TokenMap $Version" } else { $ReleaseTitle }
$releaseWorkflowFiles = @(
    "package-windows.yml",
    "package-macos.yml",
    "package-linux.yml"
)
$expectedReleaseAssetNames = Get-ExpectedReleaseAssetNames -ReleaseVersion $Version
$releaseCommitSha = ""

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
    if (-not $DryRun) {
        $releaseCommitSha = Get-CommandOutput -FilePath "git" -ArgumentList @("rev-parse", "$releaseTag^{}")
    }
    Invoke-ExternalCommand -FilePath "git" -ArgumentList @("push", "origin", $releaseTag) -Mutating
    Invoke-ExternalCommand -FilePath "gh" -ArgumentList @(
        "release", "create", $releaseTag,
        "--repo", $githubRepo,
        "--title", $releaseTitleValue,
        "--notes-file", $notesFilePath
    ) -Mutating

    if (-not $SkipNextVersionBump) {
        Invoke-SetVersion -TargetVersion $NextVersion
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("add", "--", $versionGitPath) -Mutating
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("commit", "-m", "chore(release): start $NextVersion") -Mutating
        Invoke-ExternalCommand -FilePath "git" -ArgumentList @("push", "origin", "main") -Mutating
    }

    if ($WaitForReleaseAssets) {
        if ($DryRun) {
            Write-Host "Skipping release asset wait in dry-run mode."
        }
        else {
            Wait-ForReleaseAssets `
                -Repo $githubRepo `
                -ReleaseTag $releaseTag `
                -ReleaseCommitSha $releaseCommitSha `
                -WorkflowFiles $releaseWorkflowFiles `
                -ExpectedAssetNames $expectedReleaseAssetNames `
                -TimeoutMinutes $ReleaseAssetsTimeoutMinutes `
                -PollSeconds $ReleaseAssetsPollSeconds
        }
    }
}
finally {
    if ($createdTemporaryNotesFile -and (Test-Path $notesFilePath -PathType Leaf)) {
        Remove-Item -Path $notesFilePath -Force -ErrorAction SilentlyContinue
    }
}
