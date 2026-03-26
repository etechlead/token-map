param(
    [string]$Solution = "Clever.TokenMap.sln",
    [string]$Configuration = "Debug",
    [string]$OutputRoot = ".artifacts/coverage-agent",
    [int]$TopClassCount = 10,
    [string[]]$GeneratedClassPrefixes = @("CompiledAvaloniaXaml."),
    [switch]$SkipRestore,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-RepoPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

function Write-JsonResult {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Payload,

        [Parameter(Mandatory = $true)]
        [int]$ExitCode
    )

    [Console]::Out.WriteLine(($Payload | ConvertTo-Json -Depth 12))
    exit $ExitCode
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Executable,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    & $Executable @Arguments *> $LogPath
    if ($LASTEXITCODE -ne 0) {
        throw "Step '$Name' failed. See '$LogPath'."
    }
}

function Get-TestSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ResultsDirectory
    )

    $trxFiles = @(Get-ChildItem -Path $ResultsDirectory -Recurse -Filter "*.trx" -File)
    if ($trxFiles.Count -eq 0) {
        throw "No TRX files were found under '$ResultsDirectory'."
    }

    $total = 0
    $passed = 0
    $failed = 0
    $skipped = 0

    foreach ($trxFile in $trxFiles) {
        [xml]$trx = Get-Content -LiteralPath $trxFile.FullName
        $namespaceManager = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
        $namespaceManager.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")

        $counterNode = $trx.SelectSingleNode("//t:ResultSummary/t:Counters", $namespaceManager)
        if ($null -eq $counterNode) {
            throw "TRX file '$($trxFile.FullName)' did not contain counters."
        }

        $total += [int]$counterNode.total
        $passed += [int]$counterNode.passed
        $failed +=
            [int]$counterNode.failed +
            [int]$counterNode.error +
            [int]$counterNode.timeout +
            [int]$counterNode.aborted
        $skipped +=
            [int]$counterNode.notExecuted +
            [int]$counterNode.inconclusive +
            [int]$counterNode.notRunnable
    }

    return [pscustomobject]@{
        total = $total
        passed = $passed
        failed = $failed
        skipped = $skipped
        trxFiles = @($trxFiles.FullName)
    }
}

function Get-UniqueFilesByHash {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo[]]$Files
    )

    $uniqueFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    $seenHashes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($file in $Files) {
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        if ($seenHashes.Add($hash)) {
            $uniqueFiles.Add($file)
        }
    }

    return @($uniqueFiles)
}

function Measure-LineCoverage {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rows
    )

    $coveredLines = [int](($Rows | Measure-Object -Property coveredLines -Sum).Sum)
    $coverableLines = [int](($Rows | Measure-Object -Property coverableLines -Sum).Sum)
    $uncoveredLines = $coverableLines - $coveredLines
    $lineCoverage = if ($coverableLines -eq 0) {
        0.0
    }
    else {
        [math]::Round(($coveredLines * 100.0) / $coverableLines, 1)
    }

    return [pscustomobject]@{
        coveredLines = $coveredLines
        uncoveredLines = $uncoveredLines
        coverableLines = $coverableLines
        lineCoverage = $lineCoverage
    }
}

function Get-CoverageSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SummaryJsonPath,

        [Parameter(Mandatory = $true)]
        [string[]]$GeneratedPrefixes,

        [Parameter(Mandatory = $true)]
        [int]$HotspotCount
    )

    $summaryJson = Get-Content -Path $SummaryJsonPath -Raw | ConvertFrom-Json

    $assemblies = [System.Collections.Generic.List[object]]::new()
    $classes = [System.Collections.Generic.List[object]]::new()

    foreach ($assembly in $summaryJson.coverage.assemblies) {
        $assemblies.Add([pscustomobject]@{
            name = $assembly.name
            lineCoverage = [double]$assembly.coverage
            branchCoverage = if ($null -eq $assembly.branchcoverage) { $null } else { [double]$assembly.branchcoverage }
            methodCoverage = [double]$assembly.methodcoverage
            coveredLines = [int]$assembly.coveredlines
            coverableLines = [int]$assembly.coverablelines
            uncoveredLines = [int]$assembly.coverablelines - [int]$assembly.coveredlines
        })

        foreach ($class in $assembly.classesinassembly) {
            $isGenerated = $false
            foreach ($generatedPrefix in $GeneratedPrefixes) {
                if ($class.name.StartsWith($generatedPrefix, [System.StringComparison]::Ordinal)) {
                    $isGenerated = $true
                    break
                }
            }

            $classes.Add([pscustomobject]@{
                assembly = $assembly.name
                name = $class.name
                lineCoverage = [double]$class.coverage
                branchCoverage = if ($null -eq $class.branchcoverage) { $null } else { [double]$class.branchcoverage }
                methodCoverage = [double]$class.methodcoverage
                coveredLines = [int]$class.coveredlines
                coverableLines = [int]$class.coverablelines
                uncoveredLines = [int]$class.coverablelines - [int]$class.coveredlines
                generated = $isGenerated
            })
        }
    }

    $generatedClasses = @($classes | Where-Object { $_.generated })
    $nonGeneratedClasses = @($classes | Where-Object { -not $_.generated })
    $generatedCoverage = Measure-LineCoverage -Rows $generatedClasses
    $nonGeneratedCoverage = Measure-LineCoverage -Rows $nonGeneratedClasses

    $topUncoveredClasses =
        @($nonGeneratedClasses |
            Sort-Object @{ Expression = "uncoveredLines"; Descending = $true }, @{ Expression = "lineCoverage"; Descending = $false } |
            Select-Object -First $HotspotCount -Property assembly, name, lineCoverage, branchCoverage, uncoveredLines, coverableLines)

    $zeroCoverageClasses =
        @($nonGeneratedClasses |
            Where-Object { $_.lineCoverage -eq 0 } |
            Sort-Object @{ Expression = "coverableLines"; Descending = $true }, name |
            Select-Object -Property assembly, name, coverableLines)

    return [pscustomobject]@{
        assemblies = $summaryJson.summary.assemblies
        classes = $summaryJson.summary.classes
        files = $summaryJson.summary.files
        coveredLines = [int]$summaryJson.summary.coveredlines
        uncoveredLines = [int]$summaryJson.summary.uncoveredlines
        coverableLines = [int]$summaryJson.summary.coverablelines
        totalLines = [int]$summaryJson.summary.totallines
        lineCoverage = [double]$summaryJson.summary.linecoverage
        coveredBranches = [int]$summaryJson.summary.coveredbranches
        totalBranches = [int]$summaryJson.summary.totalbranches
        branchCoverage = [double]$summaryJson.summary.branchcoverage
        coveredMethods = [int]$summaryJson.summary.coveredmethods
        totalMethods = [int]$summaryJson.summary.totalmethods
        methodCoverage = [double]$summaryJson.summary.methodcoverage
        assemblyCoverage = @($assemblies | Sort-Object name)
        topUncoveredClasses = $topUncoveredClasses
        zeroCoverageClasses = $zeroCoverageClasses
        generatedCode = [pscustomobject]@{
            excludedPrefixes = $GeneratedPrefixes
            classCount = $generatedClasses.Count
            coveredLines = $generatedCoverage.coveredLines
            uncoveredLines = $generatedCoverage.uncoveredLines
            coverableLines = $generatedCoverage.coverableLines
            lineCoverageWithoutGenerated = $nonGeneratedCoverage.lineCoverage
        }
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solutionPath = Resolve-RepoPath -Path $Solution -RepositoryRoot $repoRoot
$outputRootPath = Resolve-RepoPath -Path $OutputRoot -RepositoryRoot $repoRoot
$logsPath = Join-Path $outputRootPath "logs"
$resultsPath = Join-Path $outputRootPath "test-results"
$reportPath = Join-Path $outputRootPath "report"
$agentSummaryPath = Join-Path $outputRootPath "coverage-summary.json"

$logFiles = [ordered]@{
    toolRestore = Join-Path $logsPath "tool-restore.log"
    restore = Join-Path $logsPath "restore.log"
    build = Join-Path $logsPath "build.log"
    test = Join-Path $logsPath "test.log"
    report = Join-Path $logsPath "reportgenerator.log"
}

$currentStep = "initialize"
Push-Location $repoRoot
try {
    if (Test-Path $outputRootPath) {
        Remove-Item -Path $outputRootPath -Recurse -Force
    }

    New-Item -Path $logsPath -ItemType Directory -Force | Out-Null
    New-Item -Path $resultsPath -ItemType Directory -Force | Out-Null
    New-Item -Path $reportPath -ItemType Directory -Force | Out-Null

    $currentStep = "tool-restore"
    Invoke-Step -Name $currentStep -Executable "dotnet" -Arguments @(
        "tool",
        "restore",
        "--tool-manifest",
        ".config/dotnet-tools.json",
        "--verbosity",
        "quiet"
    ) -LogPath $logFiles.toolRestore

    if (-not $SkipRestore) {
        $currentStep = "restore"
        Invoke-Step -Name $currentStep -Executable "dotnet" -Arguments @(
            "restore",
            $solutionPath,
            "--verbosity",
            "minimal"
        ) -LogPath $logFiles.restore
    }

    if (-not $SkipBuild) {
        $currentStep = "build"
        Invoke-Step -Name $currentStep -Executable "dotnet" -Arguments @(
            "build",
            $solutionPath,
            "-c",
            $Configuration,
            "--verbosity",
            "minimal"
        ) -LogPath $logFiles.build
    }

    $currentStep = "test"
    Invoke-Step -Name $currentStep -Executable "dotnet" -Arguments @(
        "test",
        $solutionPath,
        "-c",
        $Configuration,
        "--no-build",
        "--collect",
        "XPlat Code Coverage",
        "--logger",
        "trx",
        "--results-directory",
        $resultsPath,
        "--verbosity",
        "minimal"
    ) -LogPath $logFiles.test

    $coverageReports = Get-UniqueFilesByHash -Files @(Get-ChildItem -Path $resultsPath -Recurse -Filter "coverage.cobertura.xml" -File)
    if ($coverageReports.Count -eq 0) {
        throw "No coverage reports were produced under '$resultsPath'."
    }

    $currentStep = "report"
    Invoke-Step -Name $currentStep -Executable "dotnet" -Arguments @(
        "tool",
        "run",
        "reportgenerator",
        "--",
        "-reports:$($coverageReports.FullName -join ';')",
        "-targetdir:$reportPath",
        "-reporttypes:JsonSummary;HtmlSummary;TextSummary"
    ) -LogPath $logFiles.report

    $testSummary = Get-TestSummary -ResultsDirectory $resultsPath
    $coverageSummary = Get-CoverageSummary -SummaryJsonPath (Join-Path $reportPath "Summary.json") -GeneratedPrefixes $GeneratedClassPrefixes -HotspotCount $TopClassCount

    $agentSummary = [pscustomobject]@{
        schemaVersion = 1
        success = $true
        solution = $solutionPath
        configuration = $Configuration
        outputRoot = $outputRootPath
        tests = $testSummary
        coverage = $coverageSummary
        artifacts = [pscustomobject]@{
            rawCoverageReports = @($coverageReports.FullName)
            summaryJson = Join-Path $reportPath "Summary.json"
            summaryText = Join-Path $reportPath "Summary.txt"
            summaryHtml = Join-Path $reportPath "summary.html"
            logs = [pscustomobject]$logFiles
        }
    }

    ($agentSummary | ConvertTo-Json -Depth 12) | Set-Content -Path $agentSummaryPath -Encoding UTF8
    Write-JsonResult -Payload $agentSummary -ExitCode 0
}
catch {
    $failurePayload = [pscustomobject]@{
        schemaVersion = 1
        success = $false
        failedStep = $currentStep
        message = $_.Exception.Message
        solution = $solutionPath
        configuration = $Configuration
        outputRoot = $outputRootPath
        artifacts = [pscustomobject]@{
            logs = [pscustomobject]$logFiles
        }
    }

    if (Test-Path $agentSummaryPath) {
        Remove-Item -Path $agentSummaryPath -Force
    }

    Write-JsonResult -Payload $failurePayload -ExitCode 1
}
finally {
    Pop-Location
}
