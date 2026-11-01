#requires -Version 7.0

param(
  [Parameter(Mandatory)][string]$Name,
  [string]$AdbPath = 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe',
  [string]$Serial,
  [string]$DisplayId,
  [int[]]$Crop
)

$ErrorActionPreference = 'Stop'
if ($Name -notmatch '^[a-z0-9][a-z0-9-]*$') {
  throw 'Name must contain lowercase letters, numbers, or hyphens.'
}

if ($Crop -and $Crop.Length -ne 4) {
  throw 'Crop must contain x,y,width,height in source pixels.'
}

$outputDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) 'Docs/GooglePlay/screenshots'
New-Item -ItemType Directory -Force $outputDirectory | Out-Null
$destination = Join-Path $outputDirectory "$Name.png"
if (Test-Path -LiteralPath $destination) {
  throw "Already exists: $destination. Choose a new name."
}

# Copy PNG bytes directly: text redirection in older PowerShell can corrupt PNGs.
$start = [Diagnostics.ProcessStartInfo]::new($AdbPath)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true
$start.RedirectStandardError = $true
if ($Serial) {
  $start.ArgumentList.Add('-s')
  $start.ArgumentList.Add($Serial)
}

foreach ($argument in @('exec-out', 'screencap', '-p')) {
  $start.ArgumentList.Add($argument)
}

if ($DisplayId) {
  $start.ArgumentList.Add('-d')
  $start.ArgumentList.Add($DisplayId)
}

$process = [Diagnostics.Process]::Start($start)
$buffer = [IO.MemoryStream]::new()
try {
  $errorTask = $process.StandardError.ReadToEndAsync()
  $process.StandardOutput.BaseStream.CopyTo($buffer)
  $process.WaitForExit()
  $errorText = $errorTask.GetAwaiter().GetResult()
  if ($process.ExitCode -ne 0) {
    throw "Screenshot failed: $errorText"
  }

  Add-Type -AssemblyName System.Drawing
  $buffer.Position = 0
  $source = [Drawing.Image]::FromStream($buffer)
  try {
    $region = if ($Crop) { [Drawing.Rectangle]::new($Crop[0], $Crop[1], $Crop[2], $Crop[3]) } else { [Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height) }
    if ($region.X -lt 0 -or $region.Y -lt 0 -or $region.Width -le 0 -or $region.Height -le 0 -or $region.Right -gt $source.Width -or $region.Bottom -gt $source.Height) {
      throw 'Crop lies outside the captured screen.'
    }

    $shortSide = [Math]::Min($region.Width, $region.Height)
    $longSide = [Math]::Max($region.Width, $region.Height)
    if ($shortSide -lt 320 -or $longSide -gt 3840 -or $longSide -gt 2 * $shortSide) {
      throw 'Capture does not meet Play screenshot dimensions (320-3840px and maximum 2:1 ratio). Use a suitable device/emulator.'
    }

    $result = [Drawing.Bitmap]::new($region.Width, $region.Height, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [Drawing.Graphics]::FromImage($result)
    try {
      $target = [Drawing.Rectangle]::new(0, 0, $region.Width, $region.Height)
      $graphics.DrawImage($source, $target, $region, [Drawing.GraphicsUnit]::Pixel)
      $result.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
      $graphics.Dispose()
      $result.Dispose()
    }
  }
  finally {
    $source.Dispose()
  }
}
finally {
  $buffer.Dispose()
  $process.Dispose()
}

Write-Output "Saved $destination. Review for notifications, system overlays, and private information before upload."
