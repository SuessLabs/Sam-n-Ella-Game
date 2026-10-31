#requires -Version 7.0

<#
.SYNOPSIS
Increments SamElla's fourth version component once per merged develop PR.
.DESCRIPTION
Use only in a dedicated, clean CI checkout. Fetches develop, commits the version
change, and pushes normally. A commit trailer makes reruns idempotent. Rejected
pushes are retried only when another commit advanced develop in the meantime.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [string]$RepositoryPath,

  [Parameter(Mandatory)]
  [ValidateRange(1, 2147483647)]
  [int]$PullRequestNumber,

  [Parameter(Mandatory)]
  [ValidatePattern('^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$')]
  [string]$MergeCommit,

  [ValidateRange(1, 20)]
  [int]$MaxAttempts = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = [System.IO.Path]::GetFullPath($RepositoryPath)
$projectRelativePath = 'SamElla/SamElla.csproj'
$projectPath = Join-Path $repo $projectRelativePath

function Invoke-RepositoryGit
{
  param([string[]]$Arguments, [switch]$AllowFailure)

  $lines = @(& git -C $repo @Arguments 2>&1)
  $result = [pscustomobject]@{
    ExitCode = $LASTEXITCODE
    Output = ($lines | ForEach-Object { "$_" }) -join [Environment]::NewLine
  }
  if ($result.ExitCode -ne 0 -and -not $AllowFailure)
  {
    throw "git $($Arguments[0]) failed: $($result.Output)"
  }
  return $result
}

function Read-ProjectVersion
{
  $source = [System.IO.File]::ReadAllText($projectPath)
  [xml]$document = $source
  $fileNodes = $document.SelectNodes('/Project/PropertyGroup/FileVersion')
  $assemblyNodes = $document.SelectNodes('/Project/PropertyGroup/AssemblyVersion')
  if ($fileNodes.Count -ne 1 -or $assemblyNodes.Count -ne 1)
  {
    throw 'Expected exactly one FileVersion and one AssemblyVersion in SamElla.csproj.'
  }
  $version = $fileNodes[0].InnerText.Trim()
  if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $assemblyNodes[0].InnerText.Trim() -ne $version)
  {
    throw 'FileVersion and AssemblyVersion must be matching four-part numeric versions.'
  }
  $parts = @($version.Split('.') | ForEach-Object { [int]$_ })
  if (@($parts | Where-Object { $_ -lt 0 -or $_ -gt 65534 }).Count -gt 0)
  {
    throw 'Each assembly version component must be between 0 and 65534.'
  }
  return [pscustomobject]@{ Source = $source; Version = $version; Parts = $parts }
}

function Report-Version
{
  param([string]$Version, [bool]$Updated)

  if ($env:GITHUB_OUTPUT)
  {
    $commit = (Invoke-RepositoryGit -Arguments @('rev-parse', 'HEAD')).Output
    $text = "version=$Version`ncommit=$commit`nupdated=$($Updated.ToString().ToLowerInvariant())`n"
    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, $text, [System.Text.UTF8Encoding]::new($false))
  }
}

$root = (Invoke-RepositoryGit -Arguments @('rev-parse', '--show-toplevel')).Output
if ([System.IO.Path]::GetFullPath($root) -ne $repo)
{
  throw 'RepositoryPath must point to the root of a dedicated checkout.'
}
if ((Invoke-RepositoryGit -Arguments @('status', '--porcelain')).Output.Length -ne 0)
{
  throw 'Refusing to version a dirty checkout. Use a dedicated CI checkout.'
}
$branch = Invoke-RepositoryGit -Arguments @('symbolic-ref', '--quiet', '--short', 'HEAD') -AllowFailure
if ($branch.ExitCode -eq 0 -and $branch.Output -ne 'develop')
{
  throw 'Use a dedicated develop or detached checkout; other local branches are not modified.'
}

for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++)
{
  $null = Invoke-RepositoryGit -Arguments @('fetch', '--no-tags', 'origin', '+refs/heads/develop:refs/remotes/origin/develop')
  $null = Invoke-RepositoryGit -Arguments @('switch', '--detach', 'refs/remotes/origin/develop')
  $baseCommit = (Invoke-RepositoryGit -Arguments @('rev-parse', 'HEAD')).Output
  $ancestor = Invoke-RepositoryGit -Arguments @('merge-base', '--is-ancestor', $MergeCommit, 'HEAD') -AllowFailure
  if ($ancestor.ExitCode -ne 0)
  {
    throw "Merge commit $MergeCommit is not in develop's history; no version was changed."
  }

  $marker = "^NetMan-Build-PR: $PullRequestNumber`$"
  $processed = (Invoke-RepositoryGit -Arguments @('log', '-1', '--format=%H', '--extended-regexp', "--grep=$marker", 'HEAD')).Output
  if ($processed.Length -ne 0)
  {
    # A rerun must release this PR's versioned commit, even after later merges.
    $null = Invoke-RepositoryGit -Arguments @('switch', '--detach', $processed)
    $current = Read-ProjectVersion
    Write-Host "PR #$PullRequestNumber was already versioned as $($current.Version) at $processed."
    Report-Version -Version $current.Version -Updated $false
    return
  }

  foreach ($path in @((Join-Path $repo 'SamElla'), $projectPath))
  {
    if (((Get-Item -LiteralPath $path -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
    {
      throw 'Refusing to modify a linked project path.'
    }
  }
  $null = Invoke-RepositoryGit -Arguments @('ls-files', '--error-unmatch', '--', $projectRelativePath)
  $current = Read-ProjectVersion
  if ($current.Parts[3] -eq 65534)
  {
    throw 'Build component reached 65534. Update Major.Minor.Revision and reset Build explicitly before merging again.'
  }
  $next = $current.Parts.Clone()
  $next[3]++
  $newVersion = $next -join '.'
  $updatedSource = $current.Source
  foreach ($property in @('FileVersion', 'AssemblyVersion'))
  {
    $pattern = "(<$property>\s*)$([regex]::Escape($current.Version))(\s*</$property>)"
    if ([regex]::Matches($updatedSource, $pattern).Count -ne 1)
    {
      throw "Could not locate a unique $property value without rewriting the project."
    }
    $updatedSource = [regex]::Replace($updatedSource, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{
      param($match)
      return $match.Groups[1].Value + $newVersion + $match.Groups[2].Value
    })
  }
  [System.IO.File]::WriteAllText($projectPath, $updatedSource, [System.Text.UTF8Encoding]::new($false))
  $null = Invoke-RepositoryGit -Arguments @('add', '--', $projectRelativePath)
  $null = Invoke-RepositoryGit -Arguments @('-c', 'user.name=github-actions[bot]',
    '-c', 'user.email=41898282+github-actions[bot]@users.noreply.github.com',
    'commit', '-m', "chore: bump build version to $newVersion after PR #$PullRequestNumber",
    '-m', "NetMan-Build-PR: $PullRequestNumber", '-m', "NetMan-Merge-Commit: $MergeCommit", '--', $projectRelativePath)

  $push = Invoke-RepositoryGit -Arguments @('push', 'origin', 'HEAD:refs/heads/develop') -AllowFailure
  if ($push.ExitCode -eq 0)
  {
    Write-Host "PR #${PullRequestNumber}: $($current.Version) -> $newVersion (attempt $attempt)."
    Report-Version -Version $newVersion -Updated $true
    return
  }

  $null = Invoke-RepositoryGit -Arguments @('fetch', '--no-tags', 'origin', '+refs/heads/develop:refs/remotes/origin/develop')
  $remoteCommit = (Invoke-RepositoryGit -Arguments @('rev-parse', 'refs/remotes/origin/develop')).Output
  if ($remoteCommit -eq $baseCommit)
  {
    throw "Version push was rejected without a concurrent develop update. Check contents: write permissions and develop branch rules. $($push.Output)"
  }
  if ($attempt -lt $MaxAttempts)
  {
    Write-Host "Develop advanced during the push. Retrying PR #$PullRequestNumber against the latest commit."
    Start-Sleep -Milliseconds (Get-Random -Minimum 200 -Maximum 1000)
  }
}

throw "Develop kept changing during $MaxAttempts attempts. Rerun this workflow; the PR marker prevents duplicate increments."
