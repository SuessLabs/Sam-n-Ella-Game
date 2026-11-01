#requires -Version 7.0
[CmdletBinding()]
param([switch]$IncludeIos)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$source = Get-Content (Join-Path $root 'GameVersion.props')
$expected = [string]$source.Project.PropertyGroup.FileVersion
$build = [string]$source.Project.PropertyGroup.MobileBuildNumber
if ($expected -notmatch '^\d+\.\d+\.\d+\.\d+$' -or $build -notmatch '^[1-9]\d*$' -or [long]$build -gt 2100000000)
{
  throw 'Invalid shared file version or mobile build number.'
}

function Read-BuildProperties
{
  param([string]$Project, [string[]]$ExtraArguments = @())
  $output = & dotnet msbuild (Join-Path $root $Project) -nologo '-getProperty:Version,FileVersion,AssemblyVersion,ApplicationDisplayVersion,ApplicationVersion' @ExtraArguments
  if ($LASTEXITCODE -ne 0) { throw "Could not evaluate $Project" }
  return ($output -join "`n" | ConvertFrom-Json).Properties
}

function Check-Version
{
  param($Properties, [string]$Platform, [string]$DisplayVersion)
  $packageVersion = if ($Platform -eq 'iOS') { $DisplayVersion } else { $expected }
  if ($Properties.Version -ne $packageVersion -or $Properties.FileVersion -ne $expected -or $Properties.AssemblyVersion -ne $expected)
  {
    throw "$Platform assemblies do not match $expected."
  }

  if ($DisplayVersion -and ($Properties.ApplicationDisplayVersion -ne $DisplayVersion -or $Properties.ApplicationVersion -ne $build))
  {
    throw "$Platform store metadata does not match display $DisplayVersion / build $build."
  }

  Write-Host "$Platform matches $expected (mobile build $build)."
}

Check-Version (Read-BuildProperties 'SamElla/SamElla.csproj') 'Windows' ''
$androidArguments = @('-p:MobileTargetFramework=net10.0-android', '-p:TargetFramework=net10.0-android')
Check-Version (Read-BuildProperties 'SamElla.Mobile/SamElla.Mobile.csproj' $androidArguments) 'Android' $expected
if ($IncludeIos)
{
  Check-Version (Read-BuildProperties 'SamElla.Mobile/SamElla.Mobile.csproj' @('-p:MobileTargetFramework=net10.0-ios', '-p:TargetFramework=net10.0-ios')) 'iOS' ([version]$expected).ToString(3)
}

# Preview increments run in scratch copies and must never modify the source version.
$fixtureRoot = Join-Path $root ('output/CiVersionChecks/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $fixtureRoot | Out-Null
$fixturePath = Join-Path $fixtureRoot 'GameVersion.props'
$original = [IO.File]::ReadAllText((Join-Path $root 'GameVersion.props'))
$savedOutput = $env:GITHUB_OUTPUT
try
{
  $env:GITHUB_OUTPUT = Join-Path $fixtureRoot 'outputs.txt'
  foreach ($run in @(1, 2, 2))
  {
    [IO.File]::WriteAllText($fixturePath, $original)
    & (Join-Path $root '.github/scripts/Set-CiBuildVersion.ps1') -RepositoryPath $fixtureRoot -RunNumber $run
    [xml]$result = Get-Content $fixturePath
    $version = [version]$expected
    $next = "$($version.Major).$($version.Minor).$($version.Build).$($version.Revision + $run)"
    if ($result.Project.PropertyGroup.FileVersion -ne $next -or $result.Project.PropertyGroup.AssemblyVersion -ne $next -or [long]$result.Project.PropertyGroup.MobileBuildNumber -ne ([long]$build + $run))
    {
      throw 'PR pushes/reruns did not produce the expected matching versions.'
    }
  }

  $overflow = $original.Replace($expected, '0.8.0.65534')
  [IO.File]::WriteAllText($fixturePath, $overflow)
  $rejected = $false
  try { & (Join-Path $root '.github/scripts/Set-CiBuildVersion.ps1') -RepositoryPath $fixtureRoot -RunNumber 1 } catch { $rejected = $_.Exception.Message -like '*exceeds*' }
  if (-not $rejected -or [IO.File]::ReadAllText($fixturePath) -ne $overflow)
  {
    throw 'Overflow must fail without modifying the version file.'
  }
}
finally
{
  $env:GITHUB_OUTPUT = $savedOutput
}

if ([IO.File]::ReadAllText((Join-Path $root 'GameVersion.props')) -ne $original)
{
  throw 'CI checks modified the real version file.'
}

Write-Host 'Version evaluation, PR push increments, deterministic reruns, and overflow checks passed.'
