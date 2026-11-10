param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$DestinationFolder)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing.Common, System.Drawing.Primitives, System.Private.Windows.GdiPlus, System.Private.Windows.Core -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class ForestFrameVariants
{
    public static void Build(string source, string destination)
    {
        using (var input = new Bitmap(source))
        {
            if (input.Width != 1122 || input.Height != 1402)
                throw new ArgumentException("Expected the approved 1122 x 1402 frame master.");
            for (int line = 1; line <= 4; line++)
            {
                int columns = 9 - line, rows = 11 - line;
                int width = (int)Math.Round(columns * 128.0 * 1122.0 / 842.0);
                double scale = width / 1122.0;
                int top = (int)Math.Round(180 * scale), bottom = (int)Math.Round(212 * scale);
                int fieldHeight = rows * 128;
                using (var output = new Bitmap(width, top + fieldHeight + bottom, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(output))
                {
                    g.Clear(Color.FromArgb(0, 0, 0, 0));
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using (var attributes = new ImageAttributes())
                    {
                        attributes.SetWrapMode(WrapMode.TileFlipXY);
                        // Spectators scale uniformly. Only the empty center and animal-free
                        // vertical roots change length between the four defense lines.
                        g.DrawImage(input, new Rectangle(0, 0, width, top), 0, 0, 1122, 180, GraphicsUnit.Pixel, attributes);
                        g.DrawImage(input, new Rectangle(0, top, width, fieldHeight), 0, 180, 1122, 1010, GraphicsUnit.Pixel, attributes);
                        g.DrawImage(input, new Rectangle(0, top + fieldHeight, width, bottom), 0, 1190, 1122, 212, GraphicsUnit.Pixel, attributes);
                    }
                    string name = "ForestFrame_Line" + line + "_" + columns + "x" + rows + ".png";
                    output.Save(Path.Combine(destination, name), ImageFormat.Png);
                }
            }
        }
    }
}
'@
New-Item -ItemType Directory -Path $DestinationFolder -Force | Out-Null
[ForestFrameVariants]::Build((Resolve-Path -LiteralPath $Source).Path, (Resolve-Path -LiteralPath $DestinationFolder).Path)
Get-ChildItem -LiteralPath $DestinationFolder -Filter '*.png' | Select-Object Name, Length
