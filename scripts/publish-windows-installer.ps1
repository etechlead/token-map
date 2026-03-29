param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "TokenMap",
    [string]$Publisher = "Clever.pro",
    [string]$Version = "",
    [string]$OutputRoot = ".artifacts/windows-installer",
    [string]$PublishDirectory = ""
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

function Get-ArtifactPlatformLabel {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RuntimeId
    )

    switch ($RuntimeId) {
        "win-x64" { return "windows-x64" }
        "win-arm64" { return "windows-arm64" }
        default { return $RuntimeId }
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$metadata = Get-ProjectMetadata -ProjectFilePath $projectFullPath
$artifactPlatformLabel = Get-ArtifactPlatformLabel -RuntimeId $RuntimeIdentifier
$fallbackVersion = "0.1.1-local"
$resolvedVersion =
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $Version
    }
    elseif (-not [string]::IsNullOrWhiteSpace($metadata.Version)) {
        $metadata.Version
    }
    else {
        $fallbackVersion
    }

$outputRootFullPath = Join-Path $repoRoot $OutputRoot
$installerOutputPath = Join-Path $outputRootFullPath "installer"
$issPath = Join-Path $repoRoot "packaging\windows\TokenMap.iss"
$compilerPath = Get-InnoCompilerPath
$artifactBaseName = "$AppName-$artifactPlatformLabel-$resolvedVersion-setup"
$publishDirectoryPath =
    if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
        if ([System.IO.Path]::IsPathRooted($PublishDirectory)) {
            $PublishDirectory
        }
        else {
            Join-Path $repoRoot $PublishDirectory
        }
    }
    else {
        Join-Path $outputRootFullPath "publish\$RuntimeIdentifier"
    }

if ([string]::IsNullOrWhiteSpace($PublishDirectory) -and (Test-Path $outputRootFullPath)) {
    Remove-Item -Path $outputRootFullPath -Recurse -Force
}

if (Test-Path $installerOutputPath) {
    Remove-Item -Path $installerOutputPath -Recurse -Force
}

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    New-Item -Path $publishDirectoryPath -ItemType Directory -Force | Out-Null
}
New-Item -Path $installerOutputPath -ItemType Directory -Force | Out-Null

if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    if (-not (Test-Path $publishDirectoryPath -PathType Container)) {
        throw "Publish directory was not found at '$publishDirectoryPath'."
    }
}
else {
    Write-Host "Publishing $projectFullPath for $RuntimeIdentifier..."
    dotnet publish $projectFullPath `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained true `
        -p:PublishSingleFile=false `
        -p:UseAppHost=true `
        -p:Version=$resolvedVersion `
        -o $publishDirectoryPath
}

$mainExecutablePath = Join-Path $publishDirectoryPath "$($metadata.AssemblyName).exe"
if (-not (Test-Path $mainExecutablePath -PathType Leaf)) {
    throw "Published Windows executable was not found at '$mainExecutablePath'."
}

$env:TOKENMAP_APP_NAME = $AppName
$env:TOKENMAP_APP_PUBLISHER = $Publisher
$env:TOKENMAP_APP_VERSION = $resolvedVersion
$env:TOKENMAP_APP_EXE = "$($metadata.AssemblyName).exe"
$env:TOKENMAP_ARTIFACT_BASENAME = $artifactBaseName
$env:TOKENMAP_PUBLISH_DIR = $publishDirectoryPath
$env:TOKENMAP_OUTPUT_DIR = $installerOutputPath

Write-Host "Compiling Inno Setup installer with $compilerPath..."
& $compilerPath $issPath

$installerArtifactPath = Join-Path $installerOutputPath "$artifactBaseName.exe"
if (-not (Test-Path $installerArtifactPath -PathType Leaf)) {
    throw "Expected installer was not found at '$installerArtifactPath'."
}

Write-Host "Installer created at: $installerArtifactPath"
