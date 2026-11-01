#requires -Version 7.0
<#
.SYNOPSIS
Sets one deterministic Windows/mobile preview version in a disposable CI checkout.
.DESCRIPTION
Adds the workflow run number to the checked-in build components. Reruns of the
same run use the same version when started from the same checkout. Never commits
or pushes. PR Android packages are isolated from the release application ID.
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)][string]$RepositoryPath,
  [Parameter(Mandatory)][ValidateRange(1, 65534)][int]$RunNumber
)

$ErrorActionPreference = 'Stop'
$path = Join-Path $RepositoryPath 'GameVersion.props'
$source = [IO.File]::ReadAllText($path)
[xml]$document = $source
$group = $document.Project.PropertyGroup
$version = [version]$group.FileVersion
if ($version.Revision -lt 0 -or $group.AssemblyVersion -ne $group.FileVersion)
{
  throw 'Expected matching four-part FileVersion and AssemblyVersion.'
}

$revision = [long]$version.Revision + $RunNumber
$mobileBuild = [long]$group.MobileBuildNumber + $RunNumber
if ($revision -gt 65534 -or $mobileBuild -gt 2100000000 -or $mobileBuild -lt 1)
{
  throw 'CI version exceeds the assembly or Android build-number limit. Advance the release prefix/reset its revision, or migrate the CI numbering scheme.'
}

$next = "$($version.Major).$($version.Minor).$($version.Build).$revision"
foreach ($property in @('FileVersion', 'AssemblyVersion', 'MobileBuildNumber'))
{
  $value = if ($property -eq 'MobileBuildNumber') { "$mobileBuild" } else { $next }
  $pattern = "(<$property>)[^<]+(</$property>)"
  if ([regex]::Matches($source, $pattern).Count -ne 1)
  {
    throw "Expected one $property in GameVersion.props."
  }

  $source = [regex]::Replace($source, $pattern, [Text.RegularExpressions.MatchEvaluator]{
    param($match)
    return $match.Groups[1].Value + $value + $match.Groups[2].Value
  })
}

[IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
if ($env:GITHUB_OUTPUT)
{
  [IO.File]::AppendAllText($env:GITHUB_OUTPUT, "version=$next`nmobile-build=$mobileBuild`n", [Text.UTF8Encoding]::new($false))
}

Write-Host "CI preview: Windows/Android $next; mobile build $mobileBuild. No commit or push."
