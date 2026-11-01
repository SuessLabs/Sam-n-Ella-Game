#requires -Version 7.0
[CmdletBinding()]
param(
  [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
  [ValidateSet('android-arm64', 'android-x64')][string]$Runtime = 'android-arm64',
  [ValidateRange(1, 2100000000)][int]$BuildNumber,
  [string]$JavaSdkDirectory,
  [switch]$Preview,
  [switch]$Offline
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'SamElla.Mobile/SamElla.Mobile.csproj'
$destination = Join-Path $PSScriptRoot "Publish/Mobile/$Runtime"
if ($Preview)
{
  $destination = Join-Path $PSScriptRoot "Publish/Mobile/Preview/$Runtime"
}
$arguments = @('publish', $project, '--framework', 'net10.0-android', '--configuration', $Configuration, '--runtime', $Runtime,
  '-p:MobileTargetFramework=net10.0-android', '-p:AndroidPackageFormats=apk', '--output', $destination)
if ($PSBoundParameters.ContainsKey('BuildNumber'))
{
  $arguments += "-p:ApplicationVersion=$BuildNumber"
}
if ($Preview)
{
  $arguments += '-p:ApplicationId=com.suesslabs.samella.preview', "-p:ApplicationTitle=Sam n' Ella Preview"
}
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
