param(
    [string]$ProjectRoot = "",
    [string]$OutputPath = "",
    [string]$ArtifactDirectory = "",
    [ValidateSet("tokens", "lines", "size", "refactor")]
    [string]$Metric = "refactor",
    [double]$Threshold = 25,
    [int]$WindowWidth = 0,
    [int]$WindowHeight = 0
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-ExternalCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [string[]]$ArgumentList = @()
    )

    $display = if ($ArgumentList.Count -gt 0) { "$FilePath $($ArgumentList -join ' ')" } else { $FilePath }
    Write-Host ">> $display"

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $display"
    }
}

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function Get-ImageSize {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Add-Type -AssemblyName System.Drawing

    $image = [System.Drawing.Image]::FromFile($Path)
    try {
        return [pscustomobject]@{
            Width = $image.Width
            Height = $image.Height
        }
    }
    finally {
        $image.Dispose()
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectRootValue = if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $repoRoot } else { Resolve-AbsolutePath -BasePath $repoRoot -PathValue $ProjectRoot }
$outputPathValue = if ([string]::IsNullOrWhiteSpace($OutputPath)) { Join-Path $repoRoot "docs\\readme\\screenshot.png" } else { Resolve-AbsolutePath -BasePath $repoRoot -PathValue $OutputPath }
$artifactDirectoryValue = if ([string]::IsNullOrWhiteSpace($ArtifactDirectory)) { Join-Path $repoRoot ".artifacts\\visual-harness\\readme" } else { Resolve-AbsolutePath -BasePath $repoRoot -PathValue $ArtifactDirectory }

if (-not (Test-Path $projectRootValue -PathType Container)) {
    throw "Project root was not found at '$projectRootValue'."
}

if (($WindowWidth -le 0 -or $WindowHeight -le 0) -and (Test-Path $outputPathValue -PathType Leaf)) {
    $existingImageSize = Get-ImageSize -Path $outputPathValue
    if ($WindowWidth -le 0) {
        $WindowWidth = $existingImageSize.Width
    }

    if ($WindowHeight -le 0) {
        $WindowHeight = $existingImageSize.Height
    }
}

if ($WindowWidth -le 0) {
    $WindowWidth = 1257
}

if ($WindowHeight -le 0) {
    $WindowHeight = 786
}

if (Test-Path $artifactDirectoryValue) {
    Remove-Item -LiteralPath $artifactDirectoryValue -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $artifactDirectoryValue | Out-Null

Invoke-ExternalCommand -FilePath "dotnet" -ArgumentList @(
    "run",
    "--project", (Join-Path $repoRoot "tools\\Clever.TokenMap.VisualHarness"),
    "--",
    "capture",
    "--source", "repo",
    "--project-root", $projectRootValue,
    "--theme", "dark",
    "--surface", "main",
    "--palette", "weighted",
    "--metric", $Metric,
    "--threshold", $Threshold.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--window-width", $WindowWidth.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--window-height", $WindowHeight.ToString([System.Globalization.CultureInfo]::InvariantCulture),
    "--output-dir", $artifactDirectoryValue
)

$capturedImagePath = Join-Path $artifactDirectoryValue "main.weighted.png"
if (-not (Test-Path $capturedImagePath -PathType Leaf)) {
    throw "Expected capture artifact was not produced: '$capturedImagePath'."
}

$parentDirectory = Split-Path -Parent $outputPathValue
if (-not [string]::IsNullOrWhiteSpace($parentDirectory)) {
    New-Item -ItemType Directory -Force -Path $parentDirectory | Out-Null
}

Copy-Item -LiteralPath $capturedImagePath -Destination $outputPathValue -Force

Write-Host "Saved README screenshot: $outputPathValue ($WindowWidth x $WindowHeight)"
Write-Host "Capture artifact directory: $artifactDirectoryValue"
