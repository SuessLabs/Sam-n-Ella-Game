param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$assetRoot = Join-Path $repoRoot 'Docs/GooglePlay'
$imageRoot = Join-Path $assetRoot 'images'
New-Item -ItemType Directory -Force $imageRoot | Out-Null

function Export-StoreImage {
  param([string]$Source, [string]$Destination, [int]$Width, [int]$Height, [bool]$Icon)

  $original = [System.Drawing.Image]::FromFile($Source)
  $format = if ($Icon) { [System.Drawing.Imaging.PixelFormat]::Format32bppArgb } else { [System.Drawing.Imaging.PixelFormat]::Format24bppRgb }
  $bitmap = [System.Drawing.Bitmap]::new($Width, $Height, $format)
  $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $graphics.Clear([System.Drawing.ColorTranslator]::FromHtml('#17263F'))
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    # Mechanical size/format export; preserve the supplied artwork and proportions.
    $scale = [Math]::Min($Width / $original.Width, $Height / $original.Height)
    $drawWidth = [float]($original.Width * $scale)
    $drawHeight = [float]($original.Height * $scale)
    $target = [System.Drawing.RectangleF]::new(($Width - $drawWidth) / 2, ($Height - $drawHeight) / 2, $drawWidth, $drawHeight)
    $graphics.DrawImage($original, $target)
    $bitmap.Save($Destination, [System.Drawing.Imaging.ImageFormat]::Png)
  }
  finally {
    $graphics.Dispose()
    $bitmap.Dispose()
    $original.Dispose()
  }
}

Export-StoreImage -Source (Join-Path $repoRoot 'Docs/Sam-and-Ella.png') -Destination (Join-Path $imageRoot 'store-icon-512.png') -Width 512 -Height 512 -Icon $true
Export-StoreImage -Source (Join-Path $assetRoot 'source/feature-graphic-source.png') -Destination (Join-Path $imageRoot 'feature-graphic-1024x500.png') -Width 1024 -Height 500 -Icon $false
Write-Output "Exported Google Play artwork to $imageRoot"
