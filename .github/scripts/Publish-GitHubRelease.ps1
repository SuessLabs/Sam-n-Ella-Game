#requires -Version 7.0

<#
.SYNOPSIS
Tags a tested build and publishes a develop prerelease or stable Latest release.
.DESCRIPTION
Requires GitHub CLI and GH_TOKEN with contents:write. Run only after the build,
tests, packaging, and native smoke check succeed. Failed uploads leave a draft;
reruns resume drafts or leave completed releases untouched.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)]
  [ValidatePattern('^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')]
  [string]$Repository,

  [Parameter(Mandatory)]
  [ValidatePattern('^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$')]
  [string]$Commit,

  [Parameter(Mandatory)]
  [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
  [string]$Version,

  [Parameter(Mandatory)]
  [ValidateRange(1, 2147483647)]
  [int]$PullRequestNumber,

  [ValidateSet("Develop", "Stable")]
  [string]$Channel = "Develop",

  [Parameter(Mandatory)]
  [string[]]$AssetPaths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$isPrerelease = $Channel -eq 'Develop'
$tag = if ($isPrerelease) { "v$Version-pre" } else { "v$Version" }
$prereleaseFlag = if ($isPrerelease) { '--prerelease' } else { '--prerelease=false' }
$latestFlag = if ($isPrerelease) { '--latest=false' } else { '--latest=true' }

function Invoke-ReleaseGh
{
  param([string[]]$Arguments, [switch]$AllowMissing)

  $lines = @(& gh @Arguments 2>&1)
  $code = $LASTEXITCODE
  $output = ($lines | ForEach-Object { "$_" }) -join [Environment]::NewLine
  if ($code -ne 0)
  {
    if ($AllowMissing -and $output -match 'HTTP 404')
    {
      return $null
    }

    throw "GitHub $($Arguments[0]) failed (exit code $code): $output"
  }

  return $output
}

function Get-ReleaseJson
{
  param([string]$Endpoint, [switch]$AllowMissing)

  $output = Invoke-ReleaseGh -Arguments @('api', $Endpoint) -AllowMissing:$AllowMissing
  if ($null -eq $output)
  {
    return $null
  }

  return $output | ConvertFrom-Json
}

function Assert-TagCommit
{
  param([switch]$AllowMissing)

  $reference = Get-ReleaseJson "repos/$Repository/git/ref/tags/$tag" -AllowMissing:$AllowMissing
  if ($null -eq $reference)
  {
    return $false
  }

  $object = $reference.object
  for ($depth = 0; $object.type -eq 'tag' -and $depth -lt 8; $depth++)
  {
    $object = (Get-ReleaseJson "repos/$Repository/git/tags/$($object.sha)").object
  }

  if ($object.type -ne 'commit' -or $object.sha -ne $Commit)
  {
    throw "Tag $tag does not point to the versioned commit $Commit. Existing tags are never moved."
  }

  return $true
}

$assets = @($AssetPaths | ForEach-Object {
  $item = Get-Item -LiteralPath $_ -ErrorAction Stop
  if ($item.PSIsContainer)
  {
    throw "Release asset must be a file: $_"
  }

  $item.FullName
})
$names = @($assets | ForEach-Object { [IO.Path]::GetFileName($_) })
if ($names -notcontains 'releases.win-x64.json' -or -not ($names -like '*.nupkg') -or
  -not ($names -like '*-Setup.exe') -or -not ($names -like 'SamElla-v*.zip'))
{
  throw 'A release requires the Windows installer, update package/feed, and standalone ZIP.'
}

$tagExists = Assert-TagCommit -AllowMissing
$release = Get-ReleaseJson "repos/$Repository/releases/tags/$tag" -AllowMissing
if ($null -ne $release)
{
  if ([bool]$release.prerelease -ne $isPrerelease)
  {
    throw "Release $tag already exists with a different release channel; it will not be changed."
  }

  if (-not $release.draft)
  {
    $null = Assert-TagCommit
    Write-Host "Release $tag is already published. Leaving its assets unchanged."
    return
  }
}

if (-not $tagExists)
{
  # Create the ref explicitly: a draft release need not materialize its tag yet.
  $null = Invoke-ReleaseGh -Arguments @('api', "repos/$Repository/git/refs", '--method', 'POST',
    '-f', "ref=refs/tags/$tag", '-f', "sha=$Commit")
  $null = Assert-TagCommit
}

if ($null -eq $release)
{
  $notesPath = [IO.Path]::GetTempFileName()
  try
  {
    $notes = "Automatic $Channel release after PR #$PullRequestNumber.`n`nVersion: $Version`nCommit: $Commit`n"
    [IO.File]::WriteAllText($notesPath, $notes, [Text.UTF8Encoding]::new($false))
    $null = Invoke-ReleaseGh -Arguments @('release', 'create', $tag, '--repo', $Repository,
      '--target', $Commit, '--verify-tag', '--title', $tag, '--notes-file', $notesPath, '--draft', $prereleaseFlag, '--latest=false')
  }
  finally
  {
    Remove-Item -LiteralPath $notesPath -Force
  }
}

$null = Assert-TagCommit
$null = Invoke-ReleaseGh -Arguments (@('release', 'upload', $tag, '--repo', $Repository, '--clobber') + $assets)
$null = Invoke-ReleaseGh -Arguments @('release', 'edit', $tag, '--repo', $Repository,
  '--draft=false', $prereleaseFlag, $latestFlag)
Write-Host "Published release: https://github.com/$Repository/releases/tag/$tag"
