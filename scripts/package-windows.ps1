param(
    [string]$ProjectPath = "",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "",
    [string]$Publisher = "",
    [string]$Version = "",
    [string]$InstallerOutputRoot = ".artifacts/windows-installer",
    [string]$PortableOutputRoot = ".artifacts/windows-portable"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "packaging-metadata.ps1")

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

    return @{
        AssemblyName = $assemblyName
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

function Initialize-CleanDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path $Path) {
        Remove-Item -Path $Path -Recurse -Force
    }

    New-Item -Path $Path -ItemType Directory -Force | Out-Null
}

function Invoke-Publish {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDirectory,
        [Parameter(Mandatory = $true)]
        [bool]$PublishSingleFile
    )

    Initialize-CleanDirectory -Path $PublishDirectory

    $arguments = @(
        "publish",
        $projectFullPath,
        "-c", $Configuration,
        "-r", $RuntimeIdentifier,
        "--self-contained", "true",
        "-p:UseAppHost=true",
        "-p:PublishSingleFile=$($PublishSingleFile.ToString().ToLowerInvariant())",
        "-o", $PublishDirectory
    )

    if ($PublishSingleFile) {
        $arguments += "-p:IncludeNativeLibrariesForSelfExtract=true"
    }

    if (-not [string]::IsNullOrWhiteSpace($resolvedVersion)) {
        $arguments += "-p:Version=$resolvedVersion"
    }

    & dotnet @arguments
}

function New-WindowsInstaller {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDirectory,
        [Parameter(Mandatory = $true)]
        [string]$AssemblyName,
        [Parameter(Mandatory = $true)]
        [string]$ArtifactPlatformLabel
    )

    $outputRootFullPath = Join-Path $repoRoot $InstallerOutputRoot
    $installerOutputPath = Join-Path $outputRootFullPath "installer"
    $issPath = Join-Path $repoRoot "packaging\windows\TokenMap.iss"
    $compilerPath = Get-InnoCompilerPath
    $artifactBaseName = "$AppName-$ArtifactPlatformLabel-$resolvedVersion-setup"
    $mainExecutablePath = Join-Path $PublishDirectory "$AssemblyName.exe"

    Initialize-CleanDirectory -Path $installerOutputPath

    if (-not (Test-Path $mainExecutablePath -PathType Leaf)) {
        throw "Published Windows executable was not found at '$mainExecutablePath'."
    }

    $env:TOKENMAP_APP_NAME = $AppName
    $env:TOKENMAP_APP_PUBLISHER = $Publisher
    $env:TOKENMAP_APP_VERSION = $resolvedVersion
    $env:TOKENMAP_APP_EXE = "$AssemblyName.exe"
    $env:TOKENMAP_ARTIFACT_BASENAME = $artifactBaseName
    $env:TOKENMAP_PUBLISH_DIR = $PublishDirectory
    $env:TOKENMAP_OUTPUT_DIR = $installerOutputPath
    if (-not [string]::IsNullOrWhiteSpace($windowsSetupIconPath)) {
        $env:TOKENMAP_SETUP_ICON_FILE = (Join-Path $repoRoot $windowsSetupIconPath)
    }

    Write-Host "Compiling Inno Setup installer with $compilerPath..."
    & $compilerPath $issPath

    $installerArtifactPath = Join-Path $installerOutputPath "$artifactBaseName.exe"
    if (-not (Test-Path $installerArtifactPath -PathType Leaf)) {
        throw "Expected installer was not found at '$installerArtifactPath'."
    }

    Write-Host "Installer created at: $installerArtifactPath"
}

function New-WindowsPortableArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishDirectory,
        [Parameter(Mandatory = $true)]
        [string]$AssemblyName,
        [Parameter(Mandatory = $true)]
        [string]$ArtifactPlatformLabel
    )

    $outputRootFullPath = Join-Path $repoRoot $PortableOutputRoot
    $artifactOutputPath = Join-Path $outputRootFullPath "portable"
    $artifactBaseName = "$AppName-$ArtifactPlatformLabel-$resolvedVersion-portable"
    $portableExecutablePath = Join-Path $artifactOutputPath "$artifactBaseName.exe"
    $portableArchivePath = Join-Path $artifactOutputPath "$artifactBaseName.zip"
    $publishedExecutablePath = Join-Path $PublishDirectory "$AssemblyName.exe"

    Initialize-CleanDirectory -Path $artifactOutputPath

    if (-not (Test-Path $publishedExecutablePath -PathType Leaf)) {
        throw "Published Windows portable executable was not found at '$publishedExecutablePath'."
    }

    Copy-Item -Path $publishedExecutablePath -Destination $portableExecutablePath -Force
    Compress-Archive -Path $portableExecutablePath -DestinationPath $portableArchivePath -CompressionLevel Optimal

    Write-Host "Portable executable created at: $portableExecutablePath"
    Write-Host "Portable archive created at: $portableArchivePath"
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$windowsSetupIconPath = Get-PackagingMetadataValue -RepoRoot $repoRoot -XPath "//PackagingMetadata/Windows/SetupIconPath"

if ([string]::IsNullOrWhiteSpace($ProjectPath)) {
    $ProjectPath = Get-PackagingMetadataValue -RepoRoot $repoRoot -XPath "//PackagingMetadata/ProjectPath"
}

if ([string]::IsNullOrWhiteSpace($AppName)) {
    $AppName = Get-PackagingMetadataValue -RepoRoot $repoRoot -XPath "//PackagingMetadata/AppName"
}

if ([string]::IsNullOrWhiteSpace($Publisher)) {
    $Publisher = Get-PackagingMetadataValue -RepoRoot $repoRoot -XPath "//PackagingMetadata/Publisher"
}

$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$metadata = Get-ProjectMetadata -ProjectFilePath $projectFullPath
$repoVersion = Get-RepoVersion -RepoRoot $repoRoot
$artifactPlatformLabel = Get-ArtifactPlatformLabel -RuntimeId $RuntimeIdentifier
$resolvedVersion =
    if (-not [string]::IsNullOrWhiteSpace($Version)) {
        $Version
    }
    else {
        $repoVersion
    }

$installerPublishDir = Join-Path $repoRoot ".artifacts/windows-package-inputs/installer/$RuntimeIdentifier"
$portablePublishDir = Join-Path $repoRoot ".artifacts/windows-package-inputs/portable/$RuntimeIdentifier"

Write-Host "Publishing Windows setup input from $projectFullPath..."
Invoke-Publish -PublishDirectory $installerPublishDir -PublishSingleFile:$false

Write-Host "Publishing Windows portable input from $projectFullPath..."
Invoke-Publish -PublishDirectory $portablePublishDir -PublishSingleFile:$true

New-WindowsInstaller `
    -PublishDirectory $installerPublishDir `
    -AssemblyName $metadata.AssemblyName `
    -ArtifactPlatformLabel $artifactPlatformLabel

New-WindowsPortableArchive `
    -PublishDirectory $portablePublishDir `
    -AssemblyName $metadata.AssemblyName `
    -ArtifactPlatformLabel $artifactPlatformLabel
