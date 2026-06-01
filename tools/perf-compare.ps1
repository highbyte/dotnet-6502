#!/usr/bin/env pwsh
# PowerShell sibling of tools/perf-compare.sh. Same behavior — see that file
# for the full description.
#
# Usage:
#   tools/perf-compare.ps1 [-BaselineRef <ref>]

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$BaselineRef = 'master'
)

$ErrorActionPreference = 'Stop'

$Project = 'benchmarks/Highbyte.DotNet6502.Benchmarks/Highbyte.DotNet6502.Benchmarks.csproj'
$ArtifactsDir = 'BenchmarkDotNet.Artifacts/results'

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
        Write-Output "perf-compare: running benchmarks on $Label..."
        if (Test-Path $ArtifactsDir) { Remove-Item -Recurse -Force $ArtifactsDir }
        & dotnet run -c Release --project $Project -- --filter '*HotPathBenchmarks*' --exporters github | Out-Null
        $csv = Get-ChildItem -Path $ArtifactsDir -Filter '*HotPathBenchmarks-report.csv' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $csv) { throw "perf-compare: no benchmark CSV produced for $Label" }
        Copy-Item $csv.FullName (Join-Path $OutDir "$Label.csv")
    }

    Write-Output "perf-compare: baseline = $BaselineRef, head = $HeadRef"

    & git switch --detach $BaselineRef | Out-Null
    Invoke-Benchmark 'baseline'

    & git switch --detach $HeadRef | Out-Null
    Invoke-Benchmark 'head'

    & git switch $HeadRef 2>$null | Out-Null

    function Parse-Time([string]$s) {
        if (-not $s) { return $null }
        $parts = $s -split '\s+'
        if ($parts.Count -ne 2) { return $null }
        $units = @{ 'ns' = 1.0; 'us' = 1000.0; 'ms' = 1000000.0; 's' = 1000000000.0 }
        if (-not $units.ContainsKey($parts[1])) { return $null }
        return ([double]($parts[0] -replace ',', '')) * $units[$parts[1]]
    }

    function Parse-Alloc([string]$s) {
        if (-not $s -or $s -eq '-') { return 0.0 }
        $parts = $s -split '\s+'
        if ($parts.Count -ne 2) { return $null }
        $units = @{ 'B' = 1.0; 'KB' = 1024.0; 'MB' = 1048576.0 }
        if (-not $units.ContainsKey($parts[1])) { return $null }
        return ([double]($parts[0] -replace ',', '')) * $units[$parts[1]]
    }

    $baseline = @{}
    Import-Csv (Join-Path $OutDir 'baseline.csv') | ForEach-Object { $baseline[$_.Method] = $_ }
    $head = @{}
    Import-Csv (Join-Path $OutDir 'head.csv') | ForEach-Object { $head[$_.Method] = $_ }

    $RegressionRatio = 1.05
    $fail = $false

    "{0,-45} {1,14} {2,14} {3,8} {4,10}" -f 'Method', 'baseline', 'head', 'ratio', 'alloc Δ' | Write-Output
    '-' * 95 | Write-Output

    foreach ($method in $head.Keys) {
        $h = $head[$method]
        $b = $baseline[$method]
        if (-not $b) {
            "{0,-45} {1,14} {2,14}" -f $method, '(new)', $h.Mean | Write-Output
            continue
        }
        $bMean = Parse-Time $b.Mean
        $hMean = Parse-Time $h.Mean
        $bAlloc = Parse-Alloc $b.Allocated
        $hAlloc = Parse-Alloc $h.Allocated
        $ratio = $null
        $ratioStr = '?'
        if ($bMean -and $hMean) {
            $ratio = $hMean / $bMean
            $ratioStr = '{0:N3}' -f $ratio
        }
        $allocDelta = ''
        if ($bAlloc -ne $null -and $hAlloc -ne $null) {
            $delta = $hAlloc - $bAlloc
            if ($delta -ne 0) { $allocDelta = ('{0:+0;-0;0}B' -f $delta) } else { $allocDelta = '0' }
        }
        "{0,-45} {1,14} {2,14} {3,8} {4,10}" -f $method, $b.Mean, $h.Mean, $ratioStr, $allocDelta | Write-Output
        if ($ratio -and $ratio -ge $RegressionRatio) {
            "  REGRESSION: $method is $([math]::Round(($ratio - 1) * 100, 1))% slower" | Write-Output
            $fail = $true
        }
        if ($bAlloc -eq 0 -and $hAlloc -gt 0) {
            "  REGRESSION: $method introduces $hAlloc B of allocations" | Write-Output
            $fail = $true
        }
    }

    if ($fail) { exit 1 } else { exit 0 }
}
finally {
    Remove-Item -Recurse -Force $OutDir -ErrorAction SilentlyContinue
}
