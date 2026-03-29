Set-StrictMode -Version Latest

$script:PackagingMetadataCache = @{}

function Get-PackagingMetadataPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot
    )

    return Join-Path $RepoRoot "packaging\release-metadata.xml"
}

function Get-XmlDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not $script:PackagingMetadataCache.ContainsKey($Path)) {
        if (-not (Test-Path $Path -PathType Leaf)) {
            throw "XML metadata file was not found at '$Path'."
        }

        [xml]$document = Get-Content -Path $Path
        $script:PackagingMetadataCache[$Path] = $document
    }

    return $script:PackagingMetadataCache[$Path]
}

function Get-XmlValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$XPath
    )

    $document = Get-XmlDocument -Path $Path
    $node = $document.SelectSingleNode($XPath)
    if ($null -eq $node) {
        return ""
    }

    return $node.InnerText
}

function Get-PackagingMetadataValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,
        [Parameter(Mandatory = $true)]
        [string]$XPath
    )

    $metadataPath = Get-PackagingMetadataPath -RepoRoot $RepoRoot
    return Get-XmlValue -Path $metadataPath -XPath $XPath
}
