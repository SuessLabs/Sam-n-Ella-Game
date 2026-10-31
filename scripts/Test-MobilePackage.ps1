#requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApkPath)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ApkPath))
try
{
  $manifest = $zip.GetEntry('assets/game-content.txt')
  if ($null -eq $manifest) { throw 'APK is missing its game content manifest.' }
  $reader = [IO.StreamReader]::new($manifest.Open())
  try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
  $files = @($content -split '\r?\n' | Where-Object { $_.Trim() })
  foreach ($relative in $files)
  {
    $path = 'assets/Game/' + $relative.Replace('\', '/')
    if ($null -eq $zip.GetEntry($path)) { throw "APK is missing $path" }
  }

  if ($files -notcontains 'Assets/palette.txt' -or $files -notcontains 'Levels/04-06-boss-placeholder.txt')
  {
    throw 'The APK manifest is missing required gameplay assets or campaign levels.'
  }

  Write-Host "Verified $($files.Count) bundled game files in the Android APK."
}
finally
{
  $zip.Dispose()
}
