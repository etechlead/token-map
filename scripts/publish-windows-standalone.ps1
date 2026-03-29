param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "TokenMap",
    [string]$Version = "",
    [string]$OutputRoot = ".artifacts/windows-standalone"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

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
$publishDirectoryPath = Join-Path $outputRootFullPath "publish\$RuntimeIdentifier"
$artifactOutputPath = Join-Path $outputRootFullPath "standalone"
$artifactBaseName = "$AppName-$RuntimeIdentifier-$resolvedVersion-standalone"
$publishedExecutablePath = Join-Path $publishDirectoryPath "$($metadata.AssemblyName).exe"
$standaloneArtifactPath = Join-Path $artifactOutputPath "$artifactBaseName.exe"
$standaloneArchivePath = Join-Path $artifactOutputPath "$artifactBaseName.zip"

if (Test-Path $outputRootFullPath) {
    Remove-Item -Path $outputRootFullPath -Recurse -Force
}

New-Item -Path $publishDirectoryPath -ItemType Directory -Force | Out-Null
New-Item -Path $artifactOutputPath -ItemType Directory -Force | Out-Null

Write-Host "Publishing $projectFullPath for $RuntimeIdentifier as a single-file standalone app..."
dotnet publish $projectFullPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:UseAppHost=true `
    -p:Version=$resolvedVersion `
    -o $publishDirectoryPath

if (-not (Test-Path $publishedExecutablePath -PathType Leaf)) {
    throw "Published Windows standalone executable was not found at '$publishedExecutablePath'."
}

Copy-Item -Path $publishedExecutablePath -Destination $standaloneArtifactPath -Force

if (Test-Path $standaloneArchivePath -PathType Leaf) {
    Remove-Item -Path $standaloneArchivePath -Force
}

Compress-Archive -Path $standaloneArtifactPath -DestinationPath $standaloneArchivePath -CompressionLevel Optimal

Write-Host "Standalone executable created at: $standaloneArtifactPath"
Write-Host "Standalone archive created at: $standaloneArchivePath"
