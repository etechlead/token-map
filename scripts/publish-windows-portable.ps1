param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$AppName = "TokenMap",
    [string]$Version = "",
    [string]$OutputRoot = ".artifacts/windows-portable",
    [string]$PublishDirectory = ""
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
$artifactOutputPath = Join-Path $outputRootFullPath "portable"
$artifactBaseName = "$AppName-$artifactPlatformLabel-$resolvedVersion-portable"
$portableExecutablePath = Join-Path $artifactOutputPath "$artifactBaseName.exe"
$portableArchivePath = Join-Path $artifactOutputPath "$artifactBaseName.zip"
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
$publishedExecutablePath = Join-Path $publishDirectoryPath "$($metadata.AssemblyName).exe"

if ([string]::IsNullOrWhiteSpace($PublishDirectory) -and (Test-Path $outputRootFullPath)) {
    Remove-Item -Path $outputRootFullPath -Recurse -Force
}

if (Test-Path $artifactOutputPath) {
    Remove-Item -Path $artifactOutputPath -Recurse -Force
}

if ([string]::IsNullOrWhiteSpace($PublishDirectory)) {
    New-Item -Path $publishDirectoryPath -ItemType Directory -Force | Out-Null
}
New-Item -Path $artifactOutputPath -ItemType Directory -Force | Out-Null

if (-not [string]::IsNullOrWhiteSpace($PublishDirectory)) {
    if (-not (Test-Path $publishDirectoryPath -PathType Container)) {
        throw "Publish directory was not found at '$publishDirectoryPath'."
    }
}
else {
    Write-Host "Publishing $projectFullPath for $RuntimeIdentifier as a single-file portable app..."
    dotnet publish $projectFullPath `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:UseAppHost=true `
        -p:Version=$resolvedVersion `
        -o $publishDirectoryPath
}

if (-not (Test-Path $publishedExecutablePath -PathType Leaf)) {
    throw "Published Windows portable executable was not found at '$publishedExecutablePath'."
}

Copy-Item -Path $publishedExecutablePath -Destination $portableExecutablePath -Force

if (Test-Path $portableArchivePath -PathType Leaf) {
    Remove-Item -Path $portableArchivePath -Force
}

Compress-Archive -Path $portableExecutablePath -DestinationPath $portableArchivePath -CompressionLevel Optimal

Write-Host "Portable executable created at: $portableExecutablePath"
Write-Host "Portable archive created at: $portableArchivePath"
