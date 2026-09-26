<#
.SYNOPSIS
    Draws the Claude Session Finder icon and writes it as a multi-resolution app.ico.

.DESCRIPTION
    The geometry here is the geometry of app.svg and app-16.svg — the same rectangles, circle and
    line, with the same numbers — because an .ico has to be produced by something and Windows has no
    renderer for SVG. Edit the drawing in both places, or edit it here and keep the SVG in step: the
    SVG is what is readable, this is what builds.

    Sizes below 32 are drawn from the small layout, which is snapped to whole pixels and carries
    thicker strokes. Scaling the large drawing down instead loses the lens.

    Frames up to 64 are written as 32-bit DIBs and 256 as PNG, which is the layout every version of
    Windows reads. PNG frames at small sizes are legal but not universally honoured.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File generate-app-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$IconPath,
    [string]$PreviewDirectory
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

if (-not $IconPath) {
    $IconPath = Join-Path $PSScriptRoot 'app.ico'
}

$DesignUnits = 16.0
$Sizes = @(16, 20, 24, 32, 48, 64, 256)
$SmallLayoutCeiling = 24
$PngFrameFloor = 256

$FolderBodyColour = '#FF4A90E2'
$FolderTabColour = '#FF7FB2F0'
$LensColour = '#FFFFFFFF'

function New-Brush([string]$colour) {
    $brush = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($colour))
    $brush.Freeze()
    return $brush
}

function New-Pen([string]$colour, [double]$thickness, [bool]$roundCaps) {
    $pen = New-Object System.Windows.Media.Pen((New-Brush $colour), $thickness)
    if ($roundCaps) {
        $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    }
    $pen.Freeze()
    return $pen
}

function Get-Layout([int]$size) {
    if ($size -le $SmallLayoutCeiling) {
        return [pscustomobject]@{
            TabRect       = New-Object System.Windows.Rect(1.0, 2.0, 6.0, 4.0)
            TabRadius     = 1.0
            BodyRect      = New-Object System.Windows.Rect(1.0, 4.0, 14.0, 10.0)
            BodyRadius    = 1.5
            LensCentre    = New-Object System.Windows.Point(7.6, 8.4)
            LensRadius    = 3.0
            LensThickness = 1.8
            HandleFrom    = New-Object System.Windows.Point(10.0, 10.8)
            HandleTo      = New-Object System.Windows.Point(12.4, 13.0)
            HandleWidth   = 1.8
        }
    }

    return [pscustomobject]@{
        TabRect       = New-Object System.Windows.Rect(1.0, 2.2, 6.4, 3.8)
        TabRadius     = 1.0
        BodyRect      = New-Object System.Windows.Rect(1.0, 4.1, 14.0, 9.8)
        BodyRadius    = 1.6
        LensCentre    = New-Object System.Windows.Point(7.9, 8.6)
        LensRadius    = 3.05
        LensThickness = 1.5
        HandleFrom    = New-Object System.Windows.Point(10.3, 11.0)
        HandleTo      = New-Object System.Windows.Point(12.3, 12.8)
        HandleWidth   = 1.5
    }
}

function New-IconBitmap([int]$size) {
    $layout = Get-Layout $size
    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()

    $scale = $size / $DesignUnits
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))

    $context.DrawRoundedRectangle((New-Brush $FolderTabColour), $null, $layout.TabRect, $layout.TabRadius, $layout.TabRadius)
    $context.DrawRoundedRectangle((New-Brush $FolderBodyColour), $null, $layout.BodyRect, $layout.BodyRadius, $layout.BodyRadius)
    $context.DrawEllipse($null, (New-Pen $LensColour $layout.LensThickness $false), $layout.LensCentre, $layout.LensRadius, $layout.LensRadius)
    $context.DrawLine((New-Pen $LensColour $layout.HandleWidth $true), $layout.HandleFrom, $layout.HandleTo)

    $context.Pop()
    $context.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()

    return $bitmap
}

function Get-PngBytes($bitmap) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    [void]$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object System.IO.MemoryStream
    $encoder.Save($stream)

    return , $stream.ToArray()
}

function Get-DibBytes($bitmap) {
    $size = $bitmap.PixelWidth
    $converted = New-Object System.Windows.Media.Imaging.FormatConvertedBitmap($bitmap, [System.Windows.Media.PixelFormats]::Bgra32, $null, 0.0)
    $stride = $size * 4
    $pixels = New-Object byte[] ($stride * $size)
    $converted.CopyPixels($pixels, $stride, 0)

    $maskStride = [int][math]::Ceiling($size / 32.0) * 4
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([int]40)
    $writer.Write([int]$size)
    $writer.Write([int]($size * 2))
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]0)
    $writer.Write([int](($stride * $size) + ($maskStride * $size)))
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)
    $writer.Write([int]0)

    for ($row = $size - 1; $row -ge 0; $row--) {
        $writer.Write($pixels, $row * $stride, $stride)
    }

    $writer.Write([byte[]](New-Object byte[] ($maskStride * $size)))
    $writer.Flush()

    return , $stream.ToArray()
}

function Write-Icon([string]$path, $frames) {
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    $writer.Write([int16]0)
    $writer.Write([int16]1)
    $writer.Write([int16]$frames.Count)

    $offset = 6 + (16 * $frames.Count)

    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([int16]1)
        $writer.Write([int16]32)
        $writer.Write([int]$frame.Bytes.Length)
        $writer.Write([int]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($path, $stream.ToArray())
}

if ($PreviewDirectory -and -not (Test-Path $PreviewDirectory)) {
    New-Item -ItemType Directory -Path $PreviewDirectory | Out-Null
}

$frames = New-Object System.Collections.ArrayList

foreach ($size in $Sizes) {
    $bitmap = New-IconBitmap $size
    $png = Get-PngBytes $bitmap

    if ($PreviewDirectory) {
        [System.IO.File]::WriteAllBytes((Join-Path $PreviewDirectory "app-$size.png"), [byte[]]$png)
    }

    if ($size -ge $PngFrameFloor) {
        $bytes = $png
    }
    else {
        $bytes = Get-DibBytes $bitmap
    }

    [void]$frames.Add([pscustomobject]@{ Size = $size; Bytes = $bytes })
}

Write-Icon $IconPath $frames

Write-Host "Wrote $IconPath ($((Get-Item $IconPath).Length) bytes, $($frames.Count) frames)"
