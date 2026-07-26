Add-Type -AssemblyName System.Drawing

$iconPath = Join-Path $PSScriptRoot "icon.ico"

function Make-Bitmap($size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $cx = $size / 2.0
    $pad = [Math]::Max(1, [int]($size * 0.08))

    # Circle background
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point(0, 0)),
        (New-Object System.Drawing.Point($size, $size)),
        [System.Drawing.Color]::FromArgb(0, 120, 215),
        [System.Drawing.Color]::FromArgb(0, 75, 170))
    $g.FillEllipse($brush, $pad, $pad, $size - $pad * 2, $size - $pad * 2)
    $brush.Dispose()

    $penW = [Math]::Max(2.0, $size * 0.09)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $penW)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    # Down arrow
    $top    = $size * 0.18
    $bottom = $size * 0.82
    $arrow  = $size * 0.62
    $spread = $size * 0.22

    $g.DrawLine($pen, $cx, $top, $cx, $arrow)
    $g.DrawLine($pen, $cx - $spread, $arrow - $size * 0.12, $cx, $arrow + $size * 0.06)
    $g.DrawLine($pen, $cx + $spread, $arrow - $size * 0.12, $cx, $arrow + $size * 0.06)
    $g.DrawLine($pen, $cx - $spread * 1.2, $bottom, $cx + $spread * 1.2, $bottom)

    $pen.Dispose()
    $g.Dispose()
    return $bmp
}

# Build multi-res icon (32-bit BMP format inside ICO)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$entries = @()
$imageData = New-Object System.Collections.Generic.List[byte[]]

foreach ($s in $sizes) {
    $bmp = Make-Bitmap $s
    # Create a DIB header + XOR mask + AND mask
    $ms = New-Object System.IO.MemoryStream
    
    # DIB header for 32bpp (40 bytes = BITMAPINFOHEADER)
    $bw = [System.BitConverter]::GetBytes([int]$s)
    $bh = [System.BitConverter]::GetBytes([int]($s * 2))  # height is double for ICO
    $bsize = [System.BitConverter]::GetBytes([int]($s * $s * 4 + 40 + $s * $s / 8 + $s * $s / 8))
    $boff = [System.BitConverter]::GetBytes([int]40)

    $ms.Write($bw, 0, 4)
    $ms.Write($bh, 0, 4)
    $ms.Write([System.BitConverter]::GetBytes([ushort]1), 0, 2)  # planes
    $ms.Write([System.BitConverter]::GetBytes([ushort]32), 0, 2) # bpp
    $ms.Write([System.BitConverter]::GetBytes([uint]0), 0, 4)    # compression
    $ms.Write($bsize, 0, 4)                                      # image size
    $ms.Write([byte[]]::new(16), 0, 16)                          # rest of header zeros

    # RGB pixel data (bottom-up for DIB, already BGRA)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $ms.WriteByte($c.B)
            $ms.WriteByte($c.G)
            $ms.WriteByte($c.R)
            $ms.WriteByte($c.A)
        }
    }

    # AND mask (1-bit transparency)
    $andRowBytes = [Math]::Ceiling($s / 8.0)
    for ($y = 0; $y -lt $s; $y++) {
        $row = New-Object byte[] $andRowBytes
        for ($x = 0; $x -lt $s; $x++) {
            $a = $bmp.GetPixel($x, $y).A
            if ($a -eq 0) {
                $byteIdx = [int]($x / 8)
                $bitIdx  = 7 - ($x % 8)
                $row[$byteIdx] = $row[$byteIdx] -bor (1 -shl $bitIdx)
            }
        }
        $ms.Write($row, 0, $row.Length)
    }

    $data = $ms.ToArray()
    $ms.Dispose()
    $imageData.Add($data)
    $bmp.Dispose()

    $entries += @{
        Width = if ($s -eq 256) { 0 } else { $s }
        Height = if ($s -eq 256) { 0 } else { $s }
        Data = $data
    }
}

# Write ICO file
$fs = [System.IO.File]::Create($iconPath)
$fs.Write([System.BitConverter]::GetBytes([ushort]0), 0, 2)  # reserved
$fs.Write([System.BitConverter]::GetBytes([ushort]1), 0, 2)  # type: ICO
$fs.Write([System.BitConverter]::GetBytes([ushort]$entries.Count), 0, 2)

$offset = 6 + $entries.Count * 16
foreach ($e in $entries) {
    $fs.WriteByte([byte]$e.Width)
    $fs.WriteByte([byte]$e.Height)
    $fs.WriteByte(0)  # color count
    $fs.WriteByte(0)  # reserved
    $fs.Write([System.BitConverter]::GetBytes([ushort]1), 0, 2)   # planes
    $fs.Write([System.BitConverter]::GetBytes([ushort]32), 0, 2)  # bpp
    $fs.Write([System.BitConverter]::GetBytes([uint]$e.Data.Length), 0, 4)
    $fs.Write([System.BitConverter]::GetBytes([uint]$offset), 0, 4)
    $offset += $e.Data.Length
}

foreach ($e in $entries) {
    $fs.Write($e.Data, 0, $e.Data.Length)
}

$fs.Dispose()
Write-Host "Icon created: $iconPath" -ForegroundColor Green
