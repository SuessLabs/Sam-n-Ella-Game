$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Get-PackageVersion.ps1')

$cases = @{
  '0.5.0.71' = '0.5.71'
  '0.5.0.72' = '0.5.72'
  '0.5.1.0' = '0.5.65536'
  '1.2.3' = '1.2.196608'
}
foreach ($inputVersion in $cases.Keys)
{
  $actual = Get-SamEllaPackageVersion -FileVersion $inputVersion
  if ($actual -ne $cases[$inputVersion])
  {
    throw "Unexpected mapping: $inputVersion -> $actual"
  }
}
if ([version](Get-SamEllaPackageVersion '0.5.0.65535') -ge [version](Get-SamEllaPackageVersion '0.5.1.0'))
{
  throw 'Revision rollover must increase the update package version.'
}
$rejected = $false
try { Get-SamEllaPackageVersion '0.5.0.65536' | Out-Null } catch { $rejected = $true }
if (-not $rejected) { throw 'Invalid Windows version was accepted.' }
Write-Host 'Package version mapping checks passed.'
