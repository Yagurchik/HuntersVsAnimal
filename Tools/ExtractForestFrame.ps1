param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class ForestFrameAlpha
{
    // This asset contains only dark green artwork; the generated backdrop is light gray.
    // Preserve the foreground colors and feather only the light contaminated edge pixels.
    public static void Extract(string source, string destination)
    {
        using (var input = new Bitmap(source))
        using (var output = new Bitmap(input.Width, input.Height, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < input.Height; y++)
            for (int x = 0; x < input.Width; x++)
            {
                Color p = input.GetPixel(x, y);
                double coverage = Math.Max(0.0, Math.Min(1.0, (150.0 - p.R) / 25.0));
                int alpha = (int)Math.Round(p.A * coverage);
                output.SetPixel(x, y, alpha == 0 ? Color.FromArgb(0, 0, 0, 0) : Color.FromArgb(alpha, p.R, p.G, p.B));
            }
            output.Save(destination, ImageFormat.Png);
        }
    }
}
'@
[ForestFrameAlpha]::Extract((Resolve-Path -LiteralPath $Source).Path, [IO.Path]::GetFullPath($Destination))
Get-Item -LiteralPath $Destination | Select-Object FullName, Length
