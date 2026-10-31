#requires -Version 7.0
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
  [ValidateSet('android-arm64', 'android-x64')][string]$Runtime = 'android-arm64',
  [ValidateRange(1, 2100000000)][int]$BuildNumber = 1,
  [string]$JavaSdkDirectory,
  [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SamElla.Mobile/SamElla.Mobile.csproj'
[xml]$desktop = Get-Content (Join-Path $PSScriptRoot 'SamElla/SamElla.csproj')
$version = [string]($desktop.Project.PropertyGroup.FileVersion | Where-Object { $_ } | Select-Object -First 1)
$displayVersion = ([version]$version).ToString(3)
$destination = Join-Path $PSScriptRoot "Publish/Mobile/$Runtime"
$arguments = @('publish', $project, '--framework', 'net10.0-android', '--configuration', $Configuration, '--runtime', $Runtime,
  '-p:MobileTargetFramework=net10.0-android', '-p:AndroidPackageFormats=apk',
  "-p:ApplicationDisplayVersion=$displayVersion", "-p:ApplicationVersion=$BuildNumber",
  "-p:FileVersion=$version", '--output', $destination)
if ($JavaSdkDirectory)
{
  $arguments += "-p:JavaSdkDirectory=$JavaSdkDirectory"
}
elseif ($IsWindows -and (Test-Path -LiteralPath 'C:/Program Files/Android/openjdk'))
{
  $arguments += '-p:JavaSdkDirectory=C:/Program Files/Android/openjdk'
}

if ($Offline)
{
  $arguments += '--no-restore'
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0)
{
  throw "Android build failed (exit code $LASTEXITCODE). Install the maui-android workload and restore this runtime before using -Offline."
}

Write-Host "Android APK: $destination"
Write-Host 'Local builds use the SDK development signing key. Use your own signing identity for distribution.'
