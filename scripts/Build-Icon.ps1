$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $repositoryRoot 'src\KratosLegacyCollection.App\Assets'
[xml]$source = Get-Content -LiteralPath (Join-Path $assetRoot 'kratos-legacy.svg') -Raw
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = [Collections.Generic.List[byte[]]]::new()
foreach ($size in $sizes) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))
    foreach ($path in $source.svg.path) {
        $brush = [Windows.Media.BrushConverter]::new().ConvertFromString([string]$path.fill)
        $drawing.DrawGeometry($brush, $null, [Windows.Media.Geometry]::Parse([string]$path.d))
    }
    $drawing.Pop()
    $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [IO.MemoryStream]::new()
    try { $encoder.Save($memory); $frames.Add($memory.ToArray()) } finally { $memory.Dispose() }
}
$iconFile = [IO.File]::Create((Join-Path $assetRoot 'kratos-legacy.ico'))
$writer = [IO.BinaryWriter]::new($iconFile)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
$previewRoot = Join-Path $repositoryRoot 'artifacts'
[IO.Directory]::CreateDirectory($previewRoot) | Out-Null
[IO.File]::WriteAllBytes((Join-Path $previewRoot 'icon-preview.png'), $frames[$frames.Count - 1])
$decoder = [Windows.Media.Imaging.IconBitmapDecoder]::new([uri](Join-Path $assetRoot 'kratos-legacy.ico'), [Windows.Media.Imaging.BitmapCreateOptions]::None, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
if ($decoder.Frames.Count -ne $sizes.Count -or $decoder.Frames[-1].PixelWidth -ne 256) { throw 'The generated icon failed its frame check.' }
Write-Output 'Generated and decoded seven original shield icon sizes (16-256 px).'
