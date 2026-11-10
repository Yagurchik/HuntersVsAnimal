param([string]$ProjectPath = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

public static class LichArtCutout
{
    public static void Convert(string sourcePath, string outputPath)
    {
        using (var source = new Bitmap(sourcePath))
        using (var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            int w = source.Width, h = source.Height, total = w * h;
            using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(source, 0, 0);
            var data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var pixels = new byte[total * 4];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            var outside = new bool[total];
            var queue = new int[total];
            int head = 0, tail = 0;
            for (int i = 0; i < total; i++)
            {
                if (i >= w && i < total - w && i % w != 0 && i % w != w - 1) continue;
                if (!Dark(pixels, i)) continue;
                outside[i] = true; queue[tail++] = i;
            }
            while (head < tail)
            {
                int p = queue[head++], x = p % w, y = p / w;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + (d == 0 ? -1 : d == 1 ? 1 : 0);
                    int ny = y + (d == 2 ? -1 : d == 3 ? 1 : 0);
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int n = ny * w + nx;
                    if (outside[n] || !Dark(pixels, n)) continue;
                    outside[n] = true; queue[tail++] = n;
                }
            }
            // Only remove border-connected black: sockets, mouth and ink inside
            // the character survive. Keep identical canvas/pivot for every frame.
            for (int i = 0; i < total; i++)
                if (outside[i]) { pixels[i * 4] = pixels[i * 4 + 1] = pixels[i * 4 + 2] = pixels[i * 4 + 3] = 0; }
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            bitmap.UnlockBits(data);
            using (var small = new Bitmap(512, 512, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(small))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(bitmap, new Rectangle(0, 0, 512, 512));
                }
                small.Save(outputPath, ImageFormat.Png);
            }
        }
    }

    private static bool Dark(byte[] p, int i) { return Math.Max(p[i * 4], Math.Max(p[i * 4 + 1], p[i * 4 + 2])) <= 32; }
}
'@
$lichSource = Join-Path $ProjectPath 'Assets/MonsterSprite/Lich'
$lichOutput = Join-Path $lichSource 'Prepared'
New-Item -ItemType Directory -Path $lichOutput -Force | Out-Null
Get-ChildItem -LiteralPath $lichSource -Filter '*.jpg' | ForEach-Object {
    $outputFile = Join-Path $lichOutput ($_.BaseName + '.png')
    [LichArtCutout]::Convert($_.FullName, $outputFile)
    Write-Output $outputFile
}
