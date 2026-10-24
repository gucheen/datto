Add-Type -AssemblyName System.Drawing
$frames = foreach ($size in @(16,24,32,48,64,128,256)) {
 $bitmap = [Drawing.Bitmap]::new($size,$size)
 $g = [Drawing.Graphics]::FromImage($bitmap)
 $g.SmoothingMode = 'AntiAlias'
 $g.ScaleTransform($size / 64.0, $size / 64.0)
 $g.Clear([Drawing.Color]::Transparent)
 $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(105,113,220))
 $g.FillEllipse($brush,1,1,62,62)
 $pen = [Drawing.Pen]::new([Drawing.Color]::White,3.5)
 $pen.LineJoin = 'Round'
 $g.DrawRectangle($pen,19,19,26,32)
 $g.FillRectangle($brush,25,16,14,8)
 $g.DrawRectangle($pen,25,14,14,9)
 $g.DrawLine($pen,25,32,39,32)
 $g.DrawLine($pen,25,40,35,40)
 $stream = [IO.MemoryStream]::new()
 $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
 [pscustomobject]@{ Size=$size; Data=$stream.ToArray() }
 $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $brush.Dispose(); $pen.Dispose()
}
$out = [IO.File]::Create((Join-Path $PSScriptRoot '../native/Assets/Datto.ico'))
$writer = [IO.BinaryWriter]::new($out)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
 $dim = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
 $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([uint16]0)
 $writer.Write([uint16]1); $writer.Write([uint16]32)
 $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
 $offset += $frame.Data.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
$writer.Dispose()
