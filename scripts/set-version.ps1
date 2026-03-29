param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$')]
    [string]$Version,
    [string]$VersionFile = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "packaging-metadata.ps1")

function Resolve-VersionFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [string]$RequestedPath
    )

    if ([string]::IsNullOrWhiteSpace($RequestedPath)) {
        return Get-VersionMetadataPath -RepoRoot $RepoRoot
    }

    if ([System.IO.Path]::IsPathRooted($RequestedPath)) {
        return $RequestedPath
    }

    return Join-Path $RepoRoot $RequestedPath
}

function Save-XmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [xml]$Document,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true
    $settings.IndentChars = "  "
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
    $settings.OmitXmlDeclaration = $true

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$versionFilePath = Resolve-VersionFilePath -RepoRoot $repoRoot -RequestedPath $VersionFile

if (-not (Test-Path $versionFilePath -PathType Leaf)) {
    throw "Version file was not found at '$versionFilePath'."
}

[xml]$versionDocument = Get-Content -Path $versionFilePath
$versionNode = $versionDocument.SelectSingleNode("//TokenMapVersion")
if ($null -eq $versionNode) {
    throw "TokenMapVersion node was not found in '$versionFilePath'."
}

$currentVersion = $versionNode.InnerText
if ($currentVersion -eq $Version) {
    Write-Host "Repo version is already '$Version'."
    exit 0
}

$versionNode.InnerText = $Version
Save-XmlDocument -Document $versionDocument -Path $versionFilePath

Write-Host "Updated repo version: $currentVersion -> $Version"
