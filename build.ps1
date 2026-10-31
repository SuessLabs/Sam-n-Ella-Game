#requires -Version 5.1

<#
.SYNOPSIS
Builds and packages Sam n' Ella as a size-optimized, trimmed Native AOT executable.
.DESCRIPTION
Run on Windows with the .NET 10 SDK and Visual Studio's Desktop development
with C++ workload installed. Paths are based on this script, not the current
directory. The first three FileVersion components determine the archive name.
Output: Publish/SamElla-vX.Y.Z/, Publish/SamElla-vX.Y.Z.zip, and
Publish/Velopack/<runtime>/ containing the installer, update feed, and packages.
.EXAMPLE
.\build.ps1
.EXAMPLE
.\build.ps1 -Runtime win-arm64 -SkipTests
.EXAMPLE
.\build.ps1 -Run
.EXAMPLE
.\build.ps1 -SelfSign
.EXAMPLE
.\build.ps1 -Offline
#>

[CmdletBinding()]
param(
  [ValidateSet('win-x64', 'win-arm64')]
  [string]$Runtime = 'win-x64',

  [switch]$SkipTests,

  [switch]$SkipInstaller,

  [switch]$Offline,

  [switch]$Run,

  [string]$PublishDirectory = (Join-Path $PSScriptRoot 'Publish'),

  [switch]$SelfSign,

  [string]$SigningCertificateThumbprint = '',

  [ValidatePattern('^https?://[^\s"\r\n]+$')]
  [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet
{
  param([string[]]$Arguments)

  if ($Offline)
  {
    if ($Arguments[0] -in @('build', 'test', 'publish'))
    {
      # Restore from the local package cache even when obj/ has been cleaned.
      $Arguments += @("-p:RestoreConfigFile=$offlineConfigPath", '-p:NuGetAudit=false')
    }
    elseif ($Arguments[0] -eq 'tool' -and $Arguments[1] -eq 'restore')
    {
      $Arguments += @('--configfile', $offlineConfigPath)
    }
  }

  & dotnet @Arguments | Out-Host

  if ($LASTEXITCODE -ne 0)
  {
    if ($Offline)
    {
      throw "Offline dotnet $($Arguments[0]) failed (exit code $LASTEXITCODE). Offline builds require cached dependencies and installed build tools. Run .\build.bat -Runtime $Runtime while connected first, then retry -Offline. See the output above for the missing dependency or build error. No new release was packaged."
    }

    throw "dotnet $($Arguments[0]) failed (exit code $LASTEXITCODE). No new release was packaged; see the build output above."
  }
}

function Assert-ReleaseAvailable
{
  param([string]$Executable)

  if (-not (Test-Path -LiteralPath $Executable -PathType Leaf))
  {
    return
  }

  try
  {
    $probe = [System.IO.File]::Open($Executable, [System.IO.FileMode]::Open,
      [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)

    $probe.Dispose()
  }
  catch [System.IO.IOException]
  {
    throw "Cannot replace '$Executable'. Close the game running from this release folder and retry. Another process has the executable open."
  }
  catch [System.UnauthorizedAccessException]
  {
    throw "Cannot replace '$Executable'. Close any running copy of this release and check that the file and folder are writable."
  }
}

function Assert-PublishChild
{
  param([string]$Path)

  $fullPath = [System.IO.Path]::GetFullPath($Path)
  $prefix = $publishRoot + [System.IO.Path]::DirectorySeparatorChar

  if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase))
  {
    throw "Refusing to modify a path outside Publish: $fullPath"
  }

  # Do not follow directory links when replacing or cleaning generated output.
  $candidate = $fullPath

  while ($candidate.Length -ge $publishRoot.Length)
  {
    if (Test-Path -LiteralPath $candidate)
    {
      $item = Get-Item -LiteralPath $candidate -Force

      if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0)
      {
        throw "Refusing to modify linked output: $candidate"
      }
    }

    $candidate = [System.IO.Path]::GetDirectoryName($candidate)
  }
}

if ($env:OS -ne 'Windows_NT')
{
  throw "Sam n' Ella currently has a Windows backend. Build this Native AOT release on Windows."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))
{
  throw 'The .NET 10 SDK is required. Install it and ensure dotnet is on PATH.'
}

$offlineConfigPath = Join-Path $PSScriptRoot 'scripts/NuGet.Offline.config'

if ($Offline)
{
  if ($SelfSign -or $SigningCertificateThumbprint)
  {
    throw 'Offline builds cannot use signing options because signing requires an online timestamp server. Omit -SelfSign/-SigningCertificateThumbprint, or build without -Offline.'
  }

  Write-Host 'Offline build: using cached packages only; NuGet feeds, auditing, and Velopack update checks are disabled.'
}

$signingCertificate = $null
$signTool = $null
$signParameters = $null

if ($SelfSign -or $SigningCertificateThumbprint)
{
  . (Join-Path $PSScriptRoot 'scripts/CodeSigning.ps1')
  $signTool = Get-SamEllaSignTool
  $signingCertificate = Get-SamEllaSigningCertificate -SelfSign:$SelfSign -Thumbprint $SigningCertificateThumbprint
  $signParameters = "/sha1 $($signingCertificate.Thumbprint) /s My /fd SHA256 /td SHA256 /tr $TimestampUrl"
  Write-Host "Signing with: $($signingCertificate.Subject) [$($signingCertificate.Thumbprint)]"

  if ($SelfSign)
  {
    Write-Warning 'Self-signing does not establish public Windows trust or remove SmartScreen warnings.'
    $certificateDirectory = Join-Path $PSScriptRoot 'output/Signing'
    New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null
    Export-Certificate -Cert $signingCertificate -FilePath (Join-Path $certificateDirectory 'SamElla-SelfSigned.cer') -Force | Out-Null
  }
}

$projectPath = Join-Path $PSScriptRoot 'SamElla/SamElla.csproj'
$testProjectPath = Join-Path $PSScriptRoot 'SamElla.Tests/SamElla.Tests.csproj'
$publishRoot = [System.IO.Path]::GetFullPath($PublishDirectory)

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf))
{
  throw "Project not found: $projectPath"
}

# Ask MSBuild so imported properties and future version changes are respected.
$metadataOutput = & dotnet msbuild $projectPath -nologo '-getProperty:FileVersion,AssemblyName'

if ($LASTEXITCODE -ne 0)
{
  throw 'Could not read the project version with MSBuild.'
}

$metadata = ($metadataOutput -join [Environment]::NewLine) | ConvertFrom-Json
$fileVersion = [string]$metadata.Properties.FileVersion

if ($fileVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$')
{
  throw "FileVersion must contain Major.Minor.Revision, optionally followed by a fourth component. Found: $fileVersion"
}

$versionParts = $fileVersion.Split('.')
. (Join-Path $PSScriptRoot 'scripts/Get-PackageVersion.ps1')
$packageVersion = Get-SamEllaPackageVersion -FileVersion $fileVersion
$releaseName = 'SamElla-v' + ($versionParts[0..2] -join '.')
$releasePath = Join-Path $publishRoot $releaseName
$archivePath = Join-Path $publishRoot ($releaseName + '.zip')
$stagingRoot = Join-Path $publishRoot ('.staging-' + [Guid]::NewGuid().ToString('N'))
$payloadPath = Join-Path $stagingRoot $releaseName
$stagedArchive = Join-Path $stagingRoot ($releaseName + '.zip')
$stagedInstaller = Join-Path $stagingRoot 'Velopack'
$installerPath = Join-Path $publishRoot ("Velopack/" + $Runtime)
$executableName = [string]$metadata.Properties.AssemblyName + '.exe'
Assert-PublishChild $releasePath
Assert-PublishChild $archivePath
Assert-PublishChild $stagingRoot
Assert-PublishChild $installerPath
Assert-ReleaseAvailable (Join-Path $releasePath $executableName)

Write-Host "Building $releaseName for $Runtime..."
Invoke-DotNet -Arguments @('build', $projectPath, '--configuration', 'Release', '--nologo')

if (-not $SkipTests)
{
  Push-Location $PSScriptRoot
  try
  {
    Invoke-DotNet -Arguments @('test', '--project', $testProjectPath, '--configuration', 'Release')
  }
  finally
  {
    Pop-Location
  }
}

New-Item -ItemType Directory -Path $payloadPath -Force | Out-Null

try
{
  $publishArguments = @(
    'publish', $projectPath,
    '--configuration', 'Release',
    '--runtime', $Runtime,
    '--self-contained', 'true',
    '--output', $payloadPath,
    '--nologo',
    '-p:SamEllaDistribution=true',
    '-p:PublishAot=true',
    '-p:PublishTrimmed=true',
    '-p:TrimMode=full',
    '-p:OptimizationPreference=Size',
    '-p:InvariantGlobalization=true',
    '-p:UseSizeOptimizedLinq=true',
    '-p:UseSystemResourceKeys=true',
    '-p:DebuggerSupport=false',
    '-p:EventSourceSupport=false',
    '-p:StackTraceSupport=false',
    '-p:DebugType=none',
    '-p:DebugSymbols=false',
    '-p:StripSymbols=true',
    '-p:CopyOutputSymbolsToPublishDirectory=false',
    '-p:TrimmerRemoveSymbols=true',
    '-p:TreatWarningsAsErrors=true'
  )
  Invoke-DotNet -Arguments $publishArguments

  if (-not (Test-Path -LiteralPath (Join-Path $payloadPath $executableName) -PathType Leaf))
  {
    throw 'Publish completed without producing the expected native executable.'
  }

  if (-not (Test-Path -LiteralPath (Join-Path $payloadPath 'Assets/palette.txt') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadPath 'Assets/player-splatter.sprite') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadPath 'Assets/enemy-squashed.sprite') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadPath 'Assets/brick-rubble.sprite') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadPath 'Assets/brick-dust.sprite') -PathType Leaf) -or
    -not (Test-Path -LiteralPath (Join-Path $payloadPath 'Levels') -PathType Container))
  {
    throw 'Publish is missing required game assets or levels.'
  }

  $licensePath = Join-Path $PSScriptRoot 'license.md'

  if (Test-Path -LiteralPath $licensePath -PathType Leaf)
  {
    Copy-Item -LiteralPath $licensePath -Destination $payloadPath
  }

  # Native AOT can emit a separate PDB even when managed symbols are disabled.
  foreach ($symbol in Get-ChildItem -LiteralPath $payloadPath -Recurse -File -Filter '*.pdb')
  {
    Assert-PublishChild $symbol.FullName
    Remove-Item -LiteralPath $symbol.FullName -Force
  }

  if (-not $SkipInstaller)
  {
    Push-Location $PSScriptRoot
    try
    {
      Invoke-DotNet -Arguments @('tool', 'restore', '--tool-manifest', (Join-Path $PSScriptRoot '.config/dotnet-tools.json'))
      # Native AOT has no managed entry-point metadata for vpk to inspect. The application
      # calls VelopackApp.Run before normal startup; verify its fast-exit hook in native smoke tests.
      $packArguments = @('tool', 'run', 'vpk', '--', 'pack',
        '--packId', 'SuessLabs.SamElla', '--packTitle', 'Sam-Ella',
        '--packAuthors', 'Damian Suess, Xeno Innovations, (DBA) Suess Labs',
        '--icon', (Join-Path $PSScriptRoot 'SamElla/Assets/AppIcon/app.ico'),
        '--packVersion', $packageVersion, '--packDir', $payloadPath,
        '--mainExe', $executableName, '--runtime', $Runtime,
        '--channel', $Runtime, '--outputDir', $stagedInstaller, '--skipVeloAppCheck')

      if ($Offline)
      {
        $packArguments += '--skip-updates'
      }

      if ($signingCertificate)
      {
        $packArguments += @('--signParams', $signParameters)
      }

      Invoke-DotNet -Arguments $packArguments
    }
    finally
    {
      Pop-Location
    }
    if (-not (Test-Path -LiteralPath (Join-Path $stagedInstaller ("releases." + $Runtime + '.json'))))
    {
      throw 'Velopack did not produce the expected update feed.'
    }
  }

  if ($signingCertificate)
  {
    # Velopack signs its staging copies; the standalone release needs its own signature.
    & $signTool sign /sha1 $signingCertificate.Thumbprint /s My /fd SHA256 /td SHA256 /tr $TimestampUrl (Join-Path $payloadPath $executableName)

    if ($LASTEXITCODE -ne 0)
    {
      throw 'Signing the native executable failed. Existing releases were preserved.'
    }

    Assert-SamEllaSignature -Path (Join-Path $payloadPath $executableName) -Thumbprint $signingCertificate.Thumbprint

    if (-not $SkipInstaller)
    {
      $setupPath = Join-Path $stagedInstaller ("SuessLabs.SamElla-$Runtime-Setup.exe")
      Assert-SamEllaSignature -Path $setupPath -Thumbprint $signingCertificate.Thumbprint
    }
  }

  # Include one top-level versioned directory, ready to extract and run.
  Compress-Archive -LiteralPath $payloadPath -DestinationPath $stagedArchive -CompressionLevel Optimal

  # Existing releases survive build/publish/ZIP failures; replace only after success.
  Assert-ReleaseAvailable (Join-Path $releasePath $executableName)
  if (Test-Path -LiteralPath $releasePath)
  {
    Assert-PublishChild $releasePath
    Remove-Item -LiteralPath $releasePath -Recurse -Force
  }

  if (Test-Path -LiteralPath $archivePath)
  {
    Assert-PublishChild $archivePath
    Remove-Item -LiteralPath $archivePath -Force
  }

  Move-Item -LiteralPath $payloadPath -Destination $releasePath
  Move-Item -LiteralPath $stagedArchive -Destination $archivePath
  if (-not $SkipInstaller)
  {
    if (Test-Path -LiteralPath $installerPath)
    {
      Assert-PublishChild $installerPath
      Remove-Item -LiteralPath $installerPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path (Split-Path $installerPath -Parent) -Force | Out-Null
    Move-Item -LiteralPath $stagedInstaller -Destination $installerPath
    Write-Host "Velopack $packageVersion release assets: $installerPath"
    Write-Host 'Upload all files in that directory as assets of the same GitHub Release.'
  }
  $archive = Get-Item -LiteralPath $archivePath
  $executable = Get-Item -LiteralPath (Join-Path $releasePath $executableName)
  Write-Host ("Native executable: {0:N2} MB" -f ($executable.Length / 1MB))
  Write-Host ("ZIP: {0} ({1:N2} MB)" -f $archive.FullName, ($archive.Length / 1MB))
  Write-Host "Release: $releasePath"
}
finally
{
  if (Test-Path -LiteralPath $stagingRoot)
  {
    Assert-PublishChild $stagingRoot
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
  }
}

if ($Run)
{
  Write-Host "Launching $releaseName..."
  # This is the interactive game: Hidden would suppress its first ShowWindow call.
  Start-Process -FilePath (Join-Path $releasePath $executableName) -WorkingDirectory $releasePath -WindowStyle Normal
}
