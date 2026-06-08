$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$source = "C:\Users\edmir\Downloads\fe747286-dccb-4380-8e81-db6d1728dcc9.png"
$assets = Join-Path $PSScriptRoot "..\src\FortniteCinematicSettings\Assets"
$pngPath = Join-Path $assets "app-icon.png"
$icoPath = Join-Path $assets "app-icon.ico"

New-Item -ItemType Directory -Force -Path $assets | Out-Null
Copy-Item -LiteralPath $source -Destination $pngPath -Force

$sourceImage = [System.Drawing.Image]::FromFile($source)
$sizes = @(256, 128, 64, 48, 32, 24, 16)
$frames = New-Object System.Collections.Generic.List[byte[]]

try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
            $stream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $frames.Add($stream.ToArray())
            } finally {
                $stream.Dispose()
            }
        } finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
} finally {
    $sourceImage.Dispose()
}

$output = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter $output
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)
    $offset = 6 + ($sizes.Count * 16)

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }

    foreach ($frame in $frames) {
        $writer.Write($frame)
    }
} finally {
    $writer.Dispose()
    $output.Dispose()
}

Write-Host "Generated $pngPath and $icoPath"
