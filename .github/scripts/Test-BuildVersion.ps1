#requires -Version 7.0

<#
.SYNOPSIS
Tests automatic versioning against disposable local Git repositories only.
.DESCRIPTION
Run from any directory with PowerShell 7 and Git installed. Does not contact
GitHub or change the current checkout's branch, index, version, or remotes.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$sourceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scriptPath = Join-Path $PSScriptRoot 'Update-BuildVersion.ps1'
$testRoot = Join-Path $sourceRoot ('output/VersioningChecks/' + [Guid]::NewGuid().ToString('N'))
$remote = Join-Path $testRoot 'remote.git'
$seed = Join-Path $testRoot 'seed'
$worker = Join-Path $testRoot 'worker'
$racer = Join-Path $testRoot 'racer'
$powershellPath = Join-Path $PSHOME ('pwsh' + $(if ($IsWindows) { '.exe' } else { '' }))
$checks = 0

function Invoke-TestGit
{
  param([string]$Directory, [string[]]$Arguments)
  $output = @(& git -C $Directory @Arguments 2>&1)
  if ($LASTEXITCODE -ne 0)
  {
    throw "Fixture git failed: $($output -join [Environment]::NewLine)"
  }
  return ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
}

function Check
{
  param([bool]$Condition, [string]$Message)
  if (-not $Condition)
  {
    throw "FAIL: $Message"
  }
  $script:checks++
  Write-Host "PASS: $Message"
}

function Bump
{
  param([string]$Directory, [int]$Number, [string]$Commit, [switch]$ExpectFailure, [string]$OutputPath)
  $previousOutput = $env:GITHUB_OUTPUT
  try
  {
    $env:GITHUB_OUTPUT = $OutputPath
    $output = @(& $powershellPath -NoLogo -NoProfile -File $scriptPath -RepositoryPath $Directory `
      -PullRequestNumber $Number -MergeCommit $Commit 2>&1)
    $code = $LASTEXITCODE
  }
  finally
  {
    $env:GITHUB_OUTPUT = $previousOutput
  }
  if (($ExpectFailure -and $code -eq 0) -or (-not $ExpectFailure -and $code -ne 0))
  {
    throw "Unexpected bump exit code $code`: $($output -join [Environment]::NewLine)"
  }
  return ($output | ForEach-Object { "$_" }) -join [Environment]::NewLine
}

function Remote-Version
{
  [xml]$project = Invoke-TestGit $remote @('show', 'refs/heads/develop:GameVersion.props')
  $fileVersion = $project.SelectSingleNode('/Project/PropertyGroup/FileVersion').InnerText
  $assemblyVersion = $project.SelectSingleNode('/Project/PropertyGroup/AssemblyVersion').InnerText
  Check ($fileVersion -eq $assemblyVersion) 'FileVersion and AssemblyVersion match'
  return $fileVersion
}

function Commit-Version
{
  param([string]$Version)
  $null = Invoke-TestGit $racer @('fetch', 'origin')
  $null = Invoke-TestGit $racer @('switch', '--detach', 'origin/develop')
  $path = Join-Path $racer 'GameVersion.props'
  $text = [System.IO.File]::ReadAllText($path)
  $text = [regex]::Replace($text, '(?<=<FileVersion>)[^<]+', $Version)
  $text = [regex]::Replace($text, '(?<=<AssemblyVersion>)[^<]+', $Version)
  [System.IO.File]::WriteAllText($path, $text, [System.Text.UTF8Encoding]::new($false))
  $null = Invoke-TestGit $racer @('add', 'GameVersion.props')
  $null = Invoke-TestGit $racer @('-c', 'user.name=Version Test', '-c', 'user.email=test@example.invalid', '-c', 'commit.gpgsign=false',
    'commit', '-m', "Set version $Version")
  $null = Invoke-TestGit $racer @('push', 'origin', 'HEAD:refs/heads/develop')
}

$null = New-Item -ItemType Directory -Path (Join-Path $seed 'SamElla') -Force
$null = Invoke-TestGit $testRoot @('init', '--bare', '--initial-branch=develop', $remote)
$null = Invoke-TestGit $seed @('init', '--initial-branch=develop')
$null = Invoke-TestGit $seed @('config', 'core.autocrlf', 'false')
$fixture = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <FileVersion>0.4.0.60</FileVersion>
    <AssemblyVersion>0.4.0.60</AssemblyVersion>
    <MobileBuildNumber>60</MobileBuildNumber>
  </PropertyGroup>
</Project>
'@
$fixture = $fixture.Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
[System.IO.File]::WriteAllText((Join-Path $seed 'GameVersion.props'), $fixture, [System.Text.UTF8Encoding]::new($false))
$null = Invoke-TestGit $seed @('add', 'GameVersion.props')
$null = Invoke-TestGit $seed @('-c', 'user.name=Version Test', '-c', 'user.email=test@example.invalid', '-c', 'commit.gpgsign=false',
  'commit', '-m', 'Merged PR fixture')
$mergeCommit = Invoke-TestGit $seed @('rev-parse', 'HEAD')
$null = Invoke-TestGit $seed @('remote', 'add', 'origin', $remote)
$null = Invoke-TestGit $seed @('push', 'origin', 'develop')
$null = Invoke-TestGit $testRoot @('clone', $remote, $worker)
$null = Invoke-TestGit $testRoot @('clone', $remote, $racer)
$null = Invoke-TestGit $worker @('config', 'commit.gpgsign', 'false')
$null = Invoke-TestGit $racer @('config', 'commit.gpgsign', 'false')

$null = Bump $worker 101 $mergeCommit
Check ((Remote-Version) -eq '0.4.0.61') 'A merged PR increments only the fourth component'
[xml]$mobileProject = Invoke-TestGit $remote @('show', 'develop:GameVersion.props')
Check ($mobileProject.Project.PropertyGroup.MobileBuildNumber -eq '61') 'A merged PR also increments the mobile store build number'
$afterFirst = Invoke-TestGit $remote @('rev-parse', 'develop')
$null = Bump $worker 101 $mergeCommit
Check ((Invoke-TestGit $remote @('rev-parse', 'develop')) -eq $afterFirst) 'Rerunning a processed PR creates no commit'
$blob = Invoke-TestGit $remote @('show', 'develop:GameVersion.props')
Check ($blob -eq $fixture.TrimEnd().Replace('0.4.0.60', '0.4.0.61').Replace('<MobileBuildNumber>60', '<MobileBuildNumber>61')) 'Project formatting and unrelated content are preserved'

# A pre-push hook lets the other worker win the race deterministically.
$hookPath = Join-Path $worker '.git/hooks/pre-push'
$markerPath = (Join-Path $testRoot 'race-started').Replace('\', '/')
function Shell-Quote([string]$Value)
{
  return "'" + $Value.Replace('\', '/').Replace("'", "'" + '"' + "'" + '"' + "'") + "'"
}
$hook = "#!/bin/sh`nif test ! -f $(Shell-Quote $markerPath); then`n" +
  "  touch $(Shell-Quote $markerPath)`n" +
  "  $(Shell-Quote $powershellPath) -NoLogo -NoProfile -File $(Shell-Quote $scriptPath) -RepositoryPath $(Shell-Quote $racer) -PullRequestNumber 103 -MergeCommit $mergeCommit || exit 1`nfi`n"
[System.IO.File]::WriteAllText($hookPath, $hook, [System.Text.UTF8Encoding]::new($false))
if (-not $IsWindows)
{
  & chmod +x $hookPath
  if ($LASTEXITCODE -ne 0) { throw 'Could not enable the test hook.' }
}
$raceOutput = Bump $worker 102 $mergeCommit
Check ($raceOutput.Contains('Retrying PR #102')) 'Concurrent updates cause a fresh-base retry'
Check ((Remote-Version) -eq '0.4.0.63') 'Both simultaneous merges receive exactly one increment'
foreach ($number in @(101, 102, 103))
{
  $matches = Invoke-TestGit $remote @('log', '--format=%H', '--extended-regexp', "--grep=^NetMan-Build-PR: $number`$", 'develop')
  Check (($matches -split '\r?\n').Count -eq 1) "PR #$number has exactly one marker"
}
$null = Bump $worker 102 $mergeCommit
Check ((Remote-Version) -eq '0.4.0.63') 'A raced PR remains idempotent on rerun'

$outputsPath = Join-Path $testRoot 'bump-outputs.txt'
$null = Bump $worker 101 $mergeCommit -OutputPath $outputsPath
$outputs = ConvertFrom-StringData ([IO.File]::ReadAllText($outputsPath))
Check ($outputs.version -eq '0.4.0.61') 'An older PR rerun reports its original version instead of the newest develop version'
Check ($outputs.commit -eq $afterFirst) 'Release output pins the exact original version-bump commit'
Check ($outputs.updated -eq 'false') 'An older PR rerun does not bump again'
Check ((Remote-Version) -eq '0.4.0.63') 'Reporting an older release never rewinds develop'

$path = Join-Path $worker 'GameVersion.props'
$original = [System.IO.File]::ReadAllText($path)
[System.IO.File]::AppendAllText($path, '<!-- uncommitted -->')
$failure = Bump $worker 104 $mergeCommit -ExpectFailure
Check ($failure.Contains('dirty checkout')) 'Dirty checkouts are rejected without discarding changes'
[System.IO.File]::WriteAllText($path, $original, [System.Text.UTF8Encoding]::new($false))
$failure = Bump $worker 104 ('f' * 40) -ExpectFailure
Check ($failure.Contains('not in develop')) 'A missing or unmerged commit cannot bump the version'

Commit-Version '0.4.0.65534'
$failure = Bump $worker 104 $mergeCommit -ExpectFailure
Check ($failure.Contains('reached 65534')) 'The fourth component cannot overflow the assembly version limit'
Check ((Remote-Version) -eq '0.4.0.65534') 'Overflow does not change the remote'
Commit-Version '0.5.1.0'
$null = Bump $worker 105 $mergeCommit
Check ((Remote-Version) -eq '0.5.1.1') 'A deliberate Major.Minor.Revision change is preserved'
[xml]$mobileProject = Invoke-TestGit $remote @('show', 'develop:GameVersion.props')
Check ($mobileProject.Project.PropertyGroup.MobileBuildNumber -eq '64') 'Changing the release prefix never resets the mobile store build number'

$rejectHook = Join-Path $remote 'hooks/pre-receive'
[System.IO.File]::WriteAllText($rejectHook, "#!/bin/sh`necho 'Test branch rule rejects writes' >&2`nexit 1`n", [System.Text.UTF8Encoding]::new($false))
if (-not $IsWindows)
{
  & chmod +x $rejectHook
}
$failure = Bump $worker 106 $mergeCommit -ExpectFailure
Check ($failure.Contains('Version push was rejected') -and $failure.Contains('branch rules')) 'A write rejection reports branch/token configuration instead of force-pushing'
Check ((Remote-Version) -eq '0.5.1.1') 'A rejected push leaves the server version unchanged'
Write-Host "All $checks versioning checks passed. Disposable fixtures: $testRoot"
