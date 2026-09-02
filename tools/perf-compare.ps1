#!/usr/bin/env pwsh
# PowerShell sibling of tools/perf-compare.sh. Same behavior — see that file for the full
# description. Filter via -Filter or the PERF_COMPARE_FILTER environment variable.
#
# Usage:
#   tools/perf-compare.ps1 [-BaselineRef <ref>] [-Filter '<globs>']

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$BaselineRef = 'master',
    [string]$Filter = $(if ($env:PERF_COMPARE_FILTER) { $env:PERF_COMPARE_FILTER } else { '*HotPathBenchmarks* *C64ExecuteFrameBenchmark* *C64ExecuteInstructionBenchmark*' })
)

$ErrorActionPreference = 'Stop'

$Project = 'benchmarks/Highbyte.DotNet6502.Benchmarks/Highbyte.DotNet6502.Benchmarks.csproj'
$ArtifactsDir = 'BenchmarkDotNet.Artifacts'

$RepoRoot = (& git rev-parse --show-toplevel).Trim()
Set-Location $RepoRoot

$dirty = (& git status --porcelain)
if ($dirty) {
    throw 'perf-compare: working tree is dirty -- commit or stash before running.'
}

$HeadRef = (& git rev-parse --abbrev-ref HEAD).Trim()
if ($HeadRef -eq 'HEAD') { $HeadRef = (& git rev-parse HEAD).Trim() }

$OutDir = Join-Path ([IO.Path]::GetTempPath()) ("perf-compare-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $OutDir | Out-Null

try {
    function Invoke-Benchmark([string]$Label) {
        $labelDir = Join-Path $OutDir $Label
        New-Item -ItemType Directory -Path $labelDir | Out-Null
        Write-Output "perf-compare: running benchmarks on $Label..."
        if (Test-Path $ArtifactsDir) { Remove-Item -Recurse -Force $ArtifactsDir }
        $filterArgs = $Filter -split '\s+' | Where-Object { $_ }
        & dotnet run -c Release --project $Project -- --filter @filterArgs --exporters fulljson --artifacts $ArtifactsDir | Out-Null
        $reports = Get-ChildItem -Path (Join-Path $ArtifactsDir 'results') -Filter '*-report-full.json' -ErrorAction SilentlyContinue
        if (-not $reports) { throw "perf-compare: no benchmark JSON produced for $Label" }
        foreach ($r in $reports) { Copy-Item $r.FullName $labelDir }
    }

    Write-Output "perf-compare: baseline = $BaselineRef, head = $HeadRef, filter = $Filter"

    & git switch --detach $BaselineRef | Out-Null
    Invoke-Benchmark 'baseline'

    & git switch --detach $HeadRef | Out-Null
    Invoke-Benchmark 'head'

    & git switch $HeadRef 2>$null | Out-Null

    function Read-Results([string]$Dir) {
        $rows = @{}
        foreach ($file in Get-ChildItem -Path $Dir -Filter '*-report-full.json') {
            $report = Get-Content $file.FullName -Raw | ConvertFrom-Json
            foreach ($b in $report.Benchmarks) {
                $mean = $null
                if ($b.Statistics) { $mean = [double]$b.Statistics.Mean }
                $alloc = 0.0
                if ($b.Memory -and $null -ne $b.Memory.BytesAllocatedPerOperation) { $alloc = [double]$b.Memory.BytesAllocatedPerOperation }
                $rows[$b.FullName] = @{ Mean = $mean; Alloc = $alloc }
            }
        }
        return $rows
    }

    function Short-Name([string]$FullName) {
        $idx = $FullName.IndexOf('(')
        $head = if ($idx -ge 0) { $FullName.Substring(0, $idx) } else { $FullName }
        $params = if ($idx -ge 0) { $FullName.Substring($idx) } else { '' }
        $parts = $head -split '\.'
        $tail = $parts[[Math]::Max(0, $parts.Count - 2)..($parts.Count - 1)] -join '.'
        return $tail + $params
    }

    function Format-Time($ns) {
        if ($null -eq $ns) { return '?' }
        if ($ns -ge 1e9) { return ('{0:N2} s' -f ($ns / 1e9)) }
        if ($ns -ge 1e6) { return ('{0:N2} ms' -f ($ns / 1e6)) }
        if ($ns -ge 1e3) { return ('{0:N2} us' -f ($ns / 1e3)) }
        return ('{0:N2} ns' -f $ns)
    }

    $baseline = Read-Results (Join-Path $OutDir 'baseline')
    $head = Read-Results (Join-Path $OutDir 'head')

    $RegressionRatio = 1.05
    $fail = $false

    "{0,-70} {1,12} {2,12} {3,7} {4,9}" -f 'Benchmark', 'baseline', 'head', 'ratio', 'alloc Δ' | Write-Output
    '-' * 115 | Write-Output

    foreach ($fullName in $head.Keys) {
        $name = Short-Name $fullName
        $h = $head[$fullName]
        $b = $baseline[$fullName]
        if (-not $b) {
            "{0,-70} {1,12} {2,12}" -f $name, '(new)', (Format-Time $h.Mean) | Write-Output
            continue
        }
        $ratio = $null
        $ratioStr = '?'
        if ($b.Mean -and $h.Mean) {
            $ratio = $h.Mean / $b.Mean
            $ratioStr = '{0:N3}' -f $ratio
        }
        $delta = $h.Alloc - $b.Alloc
        $allocStr = if ($delta -ne 0) { ('{0:+0;-0;0}B' -f $delta) } else { '0' }
        "{0,-70} {1,12} {2,12} {3,7} {4,9}" -f $name, (Format-Time $b.Mean), (Format-Time $h.Mean), $ratioStr, $allocStr | Write-Output
        if ($ratio -and $ratio -ge $RegressionRatio) {
            "  REGRESSION: $name is $([math]::Round(($ratio - 1) * 100, 1))% slower" | Write-Output
            $fail = $true
        }
        if ($b.Alloc -eq 0 -and $h.Alloc -gt 0) {
            "  REGRESSION: $name introduces $($h.Alloc) B of allocations" | Write-Output
            $fail = $true
        }
    }

    foreach ($fullName in $baseline.Keys) {
        if (-not $head.ContainsKey($fullName)) {
            "{0,-70} {1,12}" -f (Short-Name $fullName), '(removed)' | Write-Output
        }
    }

    if ($fail) { exit 1 } else { exit 0 }
}
finally {
    Remove-Item -Recurse -Force $OutDir -ErrorAction SilentlyContinue
}
