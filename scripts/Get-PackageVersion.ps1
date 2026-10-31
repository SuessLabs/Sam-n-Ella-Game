function Get-SamEllaPackageVersion
{
  param([Parameter(Mandatory = $true)][version]$FileVersion)

  if ($FileVersion.Major -gt 65535 -or $FileVersion.Minor -gt 65535 -or
    $FileVersion.Build -lt 0 -or $FileVersion.Build -gt 65535 -or $FileVersion.Revision -gt 65535)
  {
    throw 'FileVersion must contain three or four Windows version components (0-65535).'
  }

  # Pack the Windows revision/build pair into one monotonically increasing SemVer patch.
  # 0.5.0.71 -> 0.5.71; 0.5.1.0 -> 0.5.65536. Build increments never collide.
  $patch = ([long]$FileVersion.Build * 65536) + [Math]::Max(0, $FileVersion.Revision)
  if ($patch -gt [int]::MaxValue)
  {
    throw 'The mapped package patch exceeds the supported SemVer component range.'
  }
  return "$($FileVersion.Major).$($FileVersion.Minor).$patch"
}
