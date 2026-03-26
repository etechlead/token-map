param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "TokenMap",
    [string]$Publisher = "Clever.pro",
    [string]$Version = "0.1.0-local",
    [string]$OutputRoot = "artifacts/windows-installer"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Get-InnoCompilerPath {
    $candidatePaths = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path $candidatePath -PathType Leaf) {
            return $candidatePath
        }
    }

    throw "ISCC.exe was not found. Install Inno Setup 6 first."
}

function Get-ProjectMetadata {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectFilePath
    )

    [xml]$projectXml = Get-Content -Path $ProjectFilePath

    $assemblyNameNode = $projectXml.SelectSingleNode("//AssemblyName")
    $assemblyName = if ($null -ne $assemblyNameNode) { $assemblyNameNode.InnerText } else { $null }
    if ([string]::IsNullOrWhiteSpace($assemblyName)) {
        $assemblyName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectFilePath)
    }

    $versionNode = $projectXml.SelectSingleNode("//Version")
    $projectVersion = if ($null -ne $versionNode) { $versionNode.InnerText } else { $null }

    return @{
        AssemblyName = $assemblyName
        Version = $projectVersion
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$metadata = Get-ProjectMetadata -ProjectFilePath $projectFullPath
$resolvedVersion = if ([string]::IsNullOrWhiteSpace($metadata.Version)) { $Version } else { $metadata.Version }

$outputRootFullPath = Join-Path $repoRoot $OutputRoot
$publishDirectoryPath = Join-Path $outputRootFullPath "publish\$RuntimeIdentifier"
$installerOutputPath = Join-Path $outputRootFullPath "installer"
$issPath = Join-Path $repoRoot "packaging\windows\TokenMap.iss"
$compilerPath = Get-InnoCompilerPath

if (Test-Path $outputRootFullPath) {
    Remove-Item -Path $outputRootFullPath -Recurse -Force
}

New-Item -Path $publishDirectoryPath -ItemType Directory -Force | Out-Null
New-Item -Path $installerOutputPath -ItemType Directory -Force | Out-Null

Write-Host "Publishing $projectFullPath for $RuntimeIdentifier..."
dotnet publish $projectFullPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:UseAppHost=true `
    -o $publishDirectoryPath

$mainExecutablePath = Join-Path $publishDirectoryPath "$($metadata.AssemblyName).exe"
if (-not (Test-Path $mainExecutablePath -PathType Leaf)) {
    throw "Published Windows executable was not found at '$mainExecutablePath'."
}

$env:TOKENMAP_APP_NAME = $AppName
$env:TOKENMAP_APP_PUBLISHER = $Publisher
$env:TOKENMAP_APP_VERSION = $resolvedVersion
$env:TOKENMAP_APP_EXE = "$($metadata.AssemblyName).exe"

Write-Host "Compiling Inno Setup installer with $compilerPath..."
& $compilerPath $issPath

Write-Host "Installer created in: $installerOutputPath"
