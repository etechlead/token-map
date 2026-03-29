param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "TokenMap",
    [string]$Publisher = "Clever.pro",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$installerPublishDir = Join-Path $repoRoot ".artifacts/windows-package-inputs/installer/$RuntimeIdentifier"
$portablePublishDir = Join-Path $repoRoot ".artifacts/windows-package-inputs/portable/$RuntimeIdentifier"

function Invoke-Publish {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDirectory,
        [Parameter(Mandatory = $true)]
        [bool]$PublishSingleFile
    )

    if (Test-Path $PublishDirectory) {
        Remove-Item -Path $PublishDirectory -Recurse -Force
    }

    $arguments = @(
        'publish',
        $projectFullPath,
        '-c', $Configuration,
        '-r', $RuntimeIdentifier,
        '--self-contained', 'true',
        '-p:UseAppHost=true',
        "-p:PublishSingleFile=$($PublishSingleFile.ToString().ToLowerInvariant())",
        '-o', $PublishDirectory
    )

    if ($PublishSingleFile) {
        $arguments += '-p:IncludeNativeLibrariesForSelfExtract=true'
    }

    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $arguments += "-p:Version=$Version"
    }

    & dotnet @arguments
}

Write-Host "Publishing Windows setup input from $projectFullPath..."
Invoke-Publish -PublishDirectory $installerPublishDir -PublishSingleFile:$false

Write-Host "Publishing Windows portable input from $projectFullPath..."
Invoke-Publish -PublishDirectory $portablePublishDir -PublishSingleFile:$true

$installerArguments = @{
    ProjectPath = $ProjectPath
    Configuration = $Configuration
    RuntimeIdentifier = $RuntimeIdentifier
    AppName = $AppName
    Publisher = $Publisher
    PublishDirectory = $installerPublishDir
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $installerArguments.Version = $Version
}

& (Join-Path $PSScriptRoot "publish-windows-installer.ps1") @installerArguments

$portableArguments = @{
    ProjectPath = $ProjectPath
    Configuration = $Configuration
    RuntimeIdentifier = $RuntimeIdentifier
    AppName = $AppName
    PublishDirectory = $portablePublishDir
}

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $portableArguments.Version = $Version
}

& (Join-Path $PSScriptRoot "publish-windows-portable.ps1") @portableArguments
