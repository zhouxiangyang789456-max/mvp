param(
    [string]$SourceRoot = "$(Split-Path -Parent $PSScriptRoot)\..\地形",
    [string]$OutputRoot = "$(Split-Path -Parent $PSScriptRoot)\Assets\Resources\Battle\Terrain\Generated"
)

$ErrorActionPreference = 'Stop'

$null = [System.Drawing.Bitmap]
$runtimeDirectory = Split-Path -Parent ([System.Drawing.Bitmap].Assembly.Location)
$drawingAssemblies = Get-ChildItem -LiteralPath $runtimeDirectory -Filter '*.dll' |
    Where-Object { $_.BaseName -match '^System\.Drawing|^System\.Private\.Windows|^System\.Collections$' } |
    ForEach-Object { $_.FullName }
Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

public static class TerrainBackgroundExtractor
{
    public static void Extract(string sourcePath, string outputPath)
    {
        using (var sourceFile = new Bitmap(sourcePath))
        using (var source = new Bitmap(sourceFile.Width, sourceFile.Height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.DrawImageUnscaled(sourceFile, 0, 0);
            int width = source.Width;
            int height = source.Height;
            var rect = new Rectangle(0, 0, width, height);
            var sourceData = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var pixels = new byte[sourceData.Stride * height];
            Marshal.Copy(sourceData.Scan0, pixels, 0, pixels.Length);
            source.UnlockBits(sourceData);

            bool lightBackground = CornerLuma(pixels, sourceData.Stride, width, height) >= 128;
            var background = FloodBackground(pixels, sourceData.Stride, width, height, lightBackground);

            using (var output = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                var outputData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                var result = new byte[outputData.Stride * height];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int src = y * sourceData.Stride + x * 4;
                    int dst = y * outputData.Stride + x * 4;
                    result[dst] = pixels[src];
                    result[dst + 1] = pixels[src + 1];
                    result[dst + 2] = pixels[src + 2];
                    result[dst + 3] = background[y * width + x] ? (byte)0 : (byte)255;
                }
                Marshal.Copy(result, 0, outputData.Scan0, result.Length);
                output.UnlockBits(outputData);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                output.Save(outputPath, ImageFormat.Png);
            }
        }
    }

    static bool[] FloodBackground(byte[] pixels, int stride, int width, int height,
        bool lightBackground)
    {
        var background = new bool[width * height];
        var queue = new int[width * height];
        int queueHead = 0;
        int queueTail = 0;

        Action<int, int> seed = (x, y) =>
        {
            int index = y * width + x;
            if (background[index] || !IsBackground(pixels, stride, x, y, lightBackground)) return;
            background[index] = true;
            queue[queueTail++] = index;
        };

        for (int x = 0; x < width; x++) { seed(x, 0); seed(x, height - 1); }
        for (int y = 1; y < height - 1; y++) { seed(0, y); seed(width - 1, y); }

        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };
        while (queueHead < queueTail)
        {
            int current = queue[queueHead++];
            int x = current % width;
            int y = current / width;
            for (int d = 0; d < 4; d++)
            {
                int nx = x + dx[d];
                int ny = y + dy[d];
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int next = ny * width + nx;
                if (background[next] || !IsBackground(pixels, stride, nx, ny, lightBackground)) continue;
                background[next] = true;
                queue[queueTail++] = next;
            }
        }
        return background;
    }

    static bool IsBackground(byte[] pixels, int stride, int x, int y, bool light)
    {
        int offset = y * stride + x * 4;
        int b = pixels[offset];
        int g = pixels[offset + 1];
        int r = pixels[offset + 2];
        int max = Math.Max(r, Math.Max(g, b));
        int min = Math.Min(r, Math.Min(g, b));
        int chroma = max - min;
        int luma = (r * 54 + g * 183 + b * 19) >> 8;

        // JPEG compression gives the neutral checker a little color noise. Terrain
        // edges are much more chromatic, so flood only neutral pixels connected to
        // the canvas border; enclosed gray rocks and snow remain untouched.
        return light
            ? chroma <= 24 && luma >= 155
            : chroma <= 30 && luma <= 190;
    }

    static int CornerLuma(byte[] pixels, int stride, int width, int height)
    {
        long sum = 0;
        int count = 0;
        int sample = Math.Min(24, Math.Min(width, height));
        for (int y = 0; y < sample; y += 3)
        for (int x = 0; x < sample; x += 3)
        {
            int[] xs = { x, width - 1 - x };
            int[] ys = { y, height - 1 - y };
            for (int yi = 0; yi < 2; yi++)
            for (int xi = 0; xi < 2; xi++)
            {
                int offset = ys[yi] * stride + xs[xi] * 4;
                int b = pixels[offset];
                int g = pixels[offset + 1];
                int r = pixels[offset + 2];
                sum += (r * 54 + g * 183 + b * 19) >> 8;
                count++;
            }
        }
        return count > 0 ? (int)(sum / count) : 255;
    }
}
'@

$mapping = [ordered]@{
    '5fad7a67-85a2-42cd-9391-a804acfbf8ec.jpg' = 'terrain_plain_01.png'
    'fc9bc689-6a18-4555-968d-11715fd0a29a.jpg' = 'terrain_forest_01.png'
    '63ec0de1-1c13-4eb4-863a-7b5244ad1a0f.jpg' = 'terrain_hill_01.png'
    '0a7c1bbc-a8b7-478a-8883-65404a06be19.jpg' = 'terrain_mountain_01.png'
    'f08d0474-f843-4e86-9966-bad97f6be0e2.jpg' = 'terrain_snow_mountain_01.png'
    '65e92cb1-0566-4938-9a4d-762dfb3e8bb8.jpg' = 'terrain_desert_01.png'
    '021953f0-d1a7-4679-a0d8-f82029900043.jpg' = 'terrain_shallow_water_01.png'
    '8c679439-44f2-464d-a015-cce6461d4362.jpg' = 'terrain_ocean_01.png'
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
foreach ($entry in $mapping.GetEnumerator()) {
    $source = Join-Path $SourceRoot $entry.Key
    $output = Join-Path $OutputRoot $entry.Value
    if (-not (Test-Path -LiteralPath $source)) { throw "Missing terrain source: $source" }
    [TerrainBackgroundExtractor]::Extract($source, $output)
    Write-Host "Prepared $($entry.Value)"
}
