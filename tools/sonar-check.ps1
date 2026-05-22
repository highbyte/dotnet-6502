#!/usr/bin/env pwsh
# PowerShell sibling of tools/sonar-check.sh. Same behavior — see that file
# for the full description.
#
# Usage:
#   tools/sonar-check.ps1 [-MinSeverity INFO|MINOR|MAJOR|CRITICAL|BLOCKER]

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('INFO', 'MINOR', 'MAJOR', 'CRITICAL', 'BLOCKER')]
    [string]$MinSeverity = 'MAJOR'
)

$ErrorActionPreference = 'Stop'

$ProjectKey = if ($env:SONAR_PROJECT_KEY) { $env:SONAR_PROJECT_KEY } else { 'highbyte_dotnet-6502' }
$SonarHost  = if ($env:SONAR_HOST_URL)    { $env:SONAR_HOST_URL }    else { 'https://sonarcloud.io' }
$WorkflowFile = 'sonarscan-dotnet.yml'

foreach ($t in 'gh', 'git') {
    if (-not (Get-Command $t -ErrorAction SilentlyContinue)) { throw "Required tool missing: $t" }
}

$Branch = (& git rev-parse --abbrev-ref HEAD).Trim()
$Sha    = (& git rev-parse HEAD).Trim()
Write-Output "==> Branch: $Branch  sha: $($Sha.Substring(0, 12))  threshold: $MinSeverity"

# Fail fast if the current commit hasn't been pushed — see comment in
# tools/sonar-check.sh.
$RemoteSha = & git rev-parse --verify --quiet "refs/remotes/origin/$Branch" 2>$null
if (-not $RemoteSha) {
    Write-Error "Branch '$Branch' has not been pushed to origin.`nPush it first:  git push -u origin $Branch"
    exit 2
}
$RemoteSha = $RemoteSha.Trim()
if ($RemoteSha -ne $Sha) {
    Write-Error "Local HEAD ($($Sha.Substring(0,12))) differs from origin/$Branch ($($RemoteSha.Substring(0,12))).`nThe latest commit has not been pushed yet. Push first, then re-run:`n  git push"
    exit 2
}

$RunId = $null
for ($i = 0; $i -lt 12; $i++) {
    $runs = & gh run list --workflow=$WorkflowFile --branch=$Branch --json databaseId,headSha --limit 20 | ConvertFrom-Json
    $match = $runs | Where-Object { $_.headSha -eq $Sha } | Select-Object -First 1
    if ($match) { $RunId = $match.databaseId; break }
    Start-Sleep -Seconds 5
}

if (-not $RunId) {
    Write-Error "No $WorkflowFile run found for sha $($Sha.Substring(0, 12)) on branch $Branch after 60s.`nHint: push the branch first; the workflow triggers on push to feature/**."
    exit 2
}

Write-Output "==> Waiting for Sonar workflow run $RunId ..."
& gh run watch $RunId --exit-status | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error "Sonar workflow failed; inspect the run in GitHub Actions."
    exit 1
}

# inNewCodePeriod=true: see comment in tools/sonar-check.sh — restrict to issues
# introduced on this branch only. Set $env:SONAR_INCLUDE_PREEXISTING=1 to disable.
$NewCodeFilter = if ($env:SONAR_INCLUDE_PREEXISTING -eq '1') { '' } else { '&inNewCodePeriod=true' }
$Api = "$SonarHost/api/issues/search?componentKeys=$ProjectKey&branch=$Branch&statuses=OPEN&resolved=false&ps=500$NewCodeFilter"

$IssuesJson = $null
for ($i = 0; $i -lt 12; $i++) {
    try {
        $IssuesJson = Invoke-RestMethod -Uri $Api -ErrorAction Stop
        if ($null -ne $IssuesJson.issues) { break }
    } catch {
        Start-Sleep -Seconds 5
    }
}

if (-not $IssuesJson -or $null -eq $IssuesJson.issues) {
    Write-Error "Failed to fetch Sonar issues from $SonarHost."
    exit 2
}

$Rank = @{ 'INFO' = 0; 'MINOR' = 1; 'MAJOR' = 2; 'CRITICAL' = 3; 'BLOCKER' = 4 }
$Blocking = @($IssuesJson.issues | Where-Object { $Rank[$_.severity] -ge $Rank[$MinSeverity] })

if ($Blocking.Count -eq 0) {
    Write-Output "==> No open Sonar issues at >= $MinSeverity. Clean."
    exit 0
}

Write-Output ("==> {0} open Sonar issue(s) at >= {1}:" -f $Blocking.Count, $MinSeverity)
$Blocking | Sort-Object { $Rank[$_.severity] } -Descending | ForEach-Object {
    $file = ($_.component -replace '^[^:]+:', '')
    $line = if ($_.line) { $_.line } else { '?' }
    Write-Output "  [$($_.severity)] ${file}:${line}  $($_.rule)"
    Write-Output "      $($_.message)"
}

exit 1
