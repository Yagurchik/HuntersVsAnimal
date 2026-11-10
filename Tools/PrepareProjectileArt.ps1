param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$PreviewPath,
    [ValidateRange(0, 4096)][int]$OutputSize = 0
)

# Local background extraction, explicitly approved by the user. The generated
# artwork uses warm colors against a nearly neutral gray/white checkerboard.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Collections.Generic;

public static class ProjectileArtCutout
{
    public static string Convert(string sourcePath, string outputPath, string previewPath, int outputSize)
    {
        int width, height;
        byte[] pixels;
        using (var source = new Bitmap(sourcePath))
        using (var rgba = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            width = source.Width; height = source.Height;
            using (var graphics = Graphics.FromImage(rgba)) graphics.DrawImageUnscaled(source, 0, 0);
            var data = rgba.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            pixels = new byte[width * height * 4];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            rgba.UnlockBits(data);
        }
        int total = width * height;
        bool[] candidate = new bool[total];
        for (int i = 0; i < total; i++)
        {
            int b = pixels[i * 4], g = pixels[i * 4 + 1], r = pixels[i * 4 + 2];
            int low = Math.Min(r, Math.Min(g, b)), high = Math.Max(r, Math.Max(g, b));
            candidate[i] = low >= 100 && high - low <= 24;
        }
        bool[] seen = new bool[total], background = new bool[total];
        int[] queue = new int[total];
        for (int seed = 0; seed < total; seed++)
        {
            if (!candidate[seed] || seen[seed]) continue;
            int head = 0, tail = 1;
            queue[0] = seed; seen[seed] = true;
            bool touchesBorder = false;
            while (head < tail)
            {
                int p = queue[head++], x = p % width, y = p / width;
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1) touchesBorder = true;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int n = ny * width + nx;
                    if (candidate[n] && !seen[n]) { seen[n] = true; queue[tail++] = n; }
                }
            }
            // Remove the outside plus checkerboard islands between the flame
            // tongues and ears. Tiny isolated bright ink/cloth details survive.
            if (touchesBorder || tail >= 10)
                for (int j = 0; j < tail; j++) background[queue[j]] = true;
        }

        // A one-pixel inset removes the colored antialias fringe baked over
        // the checkerboard. At source resolution this does not alter the art's
        // readable silhouette, but avoids a light halo over dark battlefields.
        bool[] cleanBackground = (bool[])background.Clone();
        for (int i = 0; i < total; i++)
        {
            if (!background[i]) continue;
            int x = i % width, y = i / width;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                    cleanBackground[ny * width + nx] = true;
            }
        }
        background = cleanBackground;
        int transparent = 0, partial = 0;
        for (int i = 0; i < total; i++)
        {
            if (background[i]) { pixels[i * 4 + 3] = 0; transparent++; continue; }
            // Remove neutral checkerboard fringes immediately next to the
            // cutout. The dark ink outline itself stays fully opaque.
            int x = i % width, y = i / width;
            bool boundary = false;
            for (int dy = -1; dy <= 1 && !boundary; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && background[ny * width + nx])
                { boundary = true; break; }
            }
            if (!boundary) continue;
            int foregroundNeighbors = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < width && ny >= 0 && ny < height && !background[ny * width + nx])
                    foregroundNeighbors++;
            }
            pixels[i * 4 + 3] = (byte)(255 * (0.45 + 0.55 * foregroundNeighbors / 9.0));
            partial++;
            int b = pixels[i * 4], g = pixels[i * 4 + 1], r = pixels[i * 4 + 2];
            int low = Math.Min(r, Math.Min(g, b)), high = Math.Max(r, Math.Max(g, b));
            if (high - low <= 30 && low > 35)
            {
                double opacity = Math.Min(1, Math.Max(0, (155.0 - low) / 120.0));
                pixels[i * 4 + 3] = (byte)(255 * opacity);
                pixels[i * 4] = (byte)Math.Min(b, 35);
                pixels[i * 4 + 1] = (byte)Math.Min(g, 35);
                pixels[i * 4 + 2] = (byte)Math.Min(r, 35);
            }
        }
        using (var fullSize = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        {
            var data = fullSize.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            fullSize.UnlockBits(data);
            int outputWidth = outputSize > 0 ? outputSize : width;
            int outputHeight = outputSize > 0 ? (int)Math.Round(height * outputSize / (double)width) : height;
            using (var result = new Bitmap(outputWidth, outputHeight, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(result))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    if (outputSize == 0) graphics.DrawImageUnscaled(fullSize, 0, 0);
                    else graphics.DrawImage(fullSize, new Rectangle(0, 0, outputWidth, outputHeight));
                }
                result.Save(outputPath, ImageFormat.Png);
                if (!string.IsNullOrEmpty(previewPath))
                {
                    using (var preview = new Bitmap(outputWidth, outputHeight))
                    using (var graphics = Graphics.FromImage(preview))
                    {
                        graphics.Clear(Color.FromArgb(29, 36, 31));
                        graphics.DrawImageUnscaled(result, 0, 0);
                        preview.Save(previewPath, ImageFormat.Png);
                    }
                }
            }
        }
        return string.Format("{0}: RGBA (source {1}x{2}, output width {5}); {3:N0} transparent source pixels, {4:N0} antialiased edge pixels", outputPath, width, height, transparent, partial, outputSize > 0 ? outputSize : width);
    }
}
'@ -ReferencedAssemblies @('System.Drawing', 'System.Drawing.Common', 'System.Drawing.Primitives', 'System.Private.Windows.GdiPlus', 'System.Private.Windows.Core', 'System.Runtime.InteropServices')

$source = (Resolve-Path -LiteralPath $InputPath).Path
$destination = [System.IO.Path]::GetFullPath($OutputPath)
$preview = if ($PreviewPath) { [System.IO.Path]::GetFullPath($PreviewPath) } else { $null }
[ProjectileArtCutout]::Convert($source, $destination, $preview, $OutputSize)
