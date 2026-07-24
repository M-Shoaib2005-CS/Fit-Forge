# Generate FitForge PWA icons (192, 512, maskable)
$iconsDir = Join-Path $PSScriptRoot "..\wwwroot\icons"
New-Item -ItemType Directory -Force -Path $iconsDir | Out-Null

Add-Type -AssemblyName System.Drawing

function New-FitForgeIcon([int]$size, [string]$path, [bool]$maskable = $false) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(255, 13, 17, 23))

    $pad = if ($maskable) { $size * 0.22 } else { $size * 0.18 }
    $accent = [System.Drawing.Color]::FromArgb(255, 0, 229, 255)
    $brush = New-Object System.Drawing.SolidBrush $accent
    $penW = [Math]::Max(2, [int]($size / 28))
    $pen = New-Object System.Drawing.Pen $accent, $penW
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $barH = $size * 0.09
    $barY = $size - $pad - $barH
    $g.FillRectangle($brush, [int]$pad, [int]$barY, [int]($size - 2 * $pad), [int]$barH)
    $g.DrawLine($pen, [int]$pad, [int]($barY - $size * 0.02), [int]($size / 2), [int]($pad + $size * 0.08), [int]($size - $pad), [int]($barY - $size * 0.02))

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created $path"
}

New-FitForgeIcon 192 "$iconsDir\icon-192.png"
New-FitForgeIcon 512 "$iconsDir\icon-512.png"
New-FitForgeIcon 512 "$iconsDir\icon-maskable-512.png" $true
