param(
    [string]$ProjectPath = "src/Clever.TokenMap.App/Clever.TokenMap.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "osx-arm64",
    [string]$BundleName = "TokenMap",
    [string]$OutputRoot = "artifacts/macos-arm64"
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
    $version = if ($null -ne $versionNode) { $versionNode.InnerText } else { $null }

    if ([string]::IsNullOrWhiteSpace($version)) {
        $version = "1.0.0"
    }

    return @{
        AssemblyName = $assemblyName
        Version = $version
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectFullPath = (Resolve-Path (Join-Path $repoRoot $ProjectPath)).Path
$metadata = Get-ProjectMetadata -ProjectFilePath $projectFullPath

$outputRootFullPath = Join-Path $repoRoot $OutputRoot
$publishDirectoryPath = Join-Path $outputRootFullPath "publish"
$bundleDirectoryPath = Join-Path $outputRootFullPath "$BundleName.app"
$bundleContentsPath = Join-Path $bundleDirectoryPath "Contents"
$bundleMacOsPath = Join-Path $bundleContentsPath "MacOS"
$bundleResourcesPath = Join-Path $bundleContentsPath "Resources"
$publishedExecutablePath = Join-Path $publishDirectoryPath $metadata.AssemblyName

if (Test-Path $outputRootFullPath) {
    Remove-Item -Path $outputRootFullPath -Recurse -Force
}

New-Item -Path $bundleMacOsPath -ItemType Directory -Force | Out-Null
New-Item -Path $bundleResourcesPath -ItemType Directory -Force | Out-Null

Write-Host "Publishing $projectFullPath for $RuntimeIdentifier..."
dotnet publish $projectFullPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    -p:UseAppHost=true `
    -o $publishDirectoryPath

if (-not (Test-Path $publishedExecutablePath -PathType Leaf)) {
    throw "Published macOS executable was not found at '$publishedExecutablePath'."
}

Copy-Item -Path (Join-Path $publishDirectoryPath "*") -Destination $bundleMacOsPath -Recurse -Force

$infoPlistPath = Join-Path $bundleContentsPath "Info.plist"
$infoPlist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleDisplayName</key>
    <string>$BundleName</string>
    <key>CFBundleExecutable</key>
    <string>$($metadata.AssemblyName)</string>
    <key>CFBundleIdentifier</key>
    <string>dev.clever.tokenmap</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$BundleName</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$($metadata.Version)</string>
    <key>CFBundleVersion</key>
    <string>$($metadata.Version)</string>
    <key>NSHighResolutionCapable</key>
    <true />
</dict>
</plist>
"@

[System.IO.File]::WriteAllText(
    $infoPlistPath,
    $infoPlist,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "macOS bundle created at: $bundleDirectoryPath"
Write-Host "Executable path inside bundle: $bundleMacOsPath\$($metadata.AssemblyName)"
Write-Host "If a direct copy from Windows loses execute permissions, run this once on the Mac:"
Write-Host "chmod +x `"$BundleName.app/Contents/MacOS/$($metadata.AssemblyName)`""
