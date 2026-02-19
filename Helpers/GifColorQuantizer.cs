using System.Drawing;
using System.Drawing.Imaging;

namespace Mif.Helpers;

public static class GifColorQuantizer
{
    public static Bitmap Quantize(Bitmap source, int maxColors)
    {
        if (maxColors <= 0 || maxColors > 256) maxColors = 256;
        Bitmap? toDispose = null;
        if (source.PixelFormat != PixelFormat.Format32bppArgb && source.PixelFormat != PixelFormat.Format24bppRgb)
        {
            toDispose = CloneTo32bpp(source);
            source = toDispose;
        }
        try
        {
            return QuantizeCore(source, maxColors);
        }
        finally
        {
            toDispose?.Dispose();
        }
    }

    private static Bitmap QuantizeCore(Bitmap source, int maxColors)
    {
        int w = source.Width;
        int h = source.Height;
        var rect = new Rectangle(0, 0, w, h);
        var bd = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = bd.Stride;
            int len = stride * h;
            var bytes = new byte[len];
            System.Runtime.InteropServices.Marshal.Copy(bd.Scan0, bytes, 0, len);

            var pixels = new List<uint>();
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x * 4;
                    uint b = bytes[i], g = bytes[i + 1], r = bytes[i + 2], a = bytes[i + 3];
                    pixels.Add((a << 24) | (r << 16) | (g << 8) | b);
                }
            }

            var palette = BuildPalette(pixels, maxColors);
            return CreateIndexedBitmap(bytes, w, h, stride, palette);
        }
        finally
        {
            source.UnlockBits(bd);
        }
    }

    private static Bitmap CloneTo32bpp(Bitmap source)
    {
        var b = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
            g.DrawImage(source, 0, 0);
        return b;
    }

    private static List<uint> BuildPalette(List<uint> pixels, int maxColors)
    {
        var unique = new HashSet<uint>(pixels);
        var list = unique.ToList();
        if (list.Count <= maxColors)
            return list;

        return MedianCut(list, maxColors);
    }

    private static List<uint> MedianCut(List<uint> colors, int targetCount)
    {
        var buckets = new List<List<uint>> { colors };
        while (buckets.Count < targetCount)
        {
            int maxSpread = -1;
            int splitIndex = 0;
            byte splitChannel = 0;
            for (int i = 0; i < buckets.Count; i++)
            {
                var b = buckets[i];
                if (b.Count < 2) continue;
                (byte channel, int spread) = GetLargestSpread(b);
                if (spread > maxSpread)
                {
                    maxSpread = spread;
                    splitIndex = i;
                    splitChannel = channel;
                }
            }
            if (maxSpread <= 0) break;
            var toSplit = buckets[splitIndex];
            toSplit.Sort((a, b) => GetChannel(a, splitChannel).CompareTo(GetChannel(b, splitChannel)));
            int mid = toSplit.Count / 2;
            var low = toSplit.Take(mid).ToList();
            var high = toSplit.Skip(mid).ToList();
            buckets.RemoveAt(splitIndex);
            buckets.Insert(splitIndex, high);
            buckets.Insert(splitIndex, low);
        }

        return buckets.Select(bucket =>
        {
            long ar = 0, ag = 0, ab = 0, aa = 0;
            int n = bucket.Count;
            foreach (uint c in bucket)
            {
                aa += (c >> 24) & 0xff;
                ar += (c >> 16) & 0xff;
                ag += (c >> 8) & 0xff;
                ab += c & 0xff;
            }
            return (uint)((n > 0 ? (aa / n) : 0) << 24 |
                         (n > 0 ? (ar / n) : 0) << 16 |
                         (n > 0 ? (ag / n) : 0) << 8 |
                         (n > 0 ? (ab / n) : 0));
        }).ToList();
    }

    private static (byte channel, int spread) GetLargestSpread(List<uint> colors)
    {
        int rMin = 255, rMax = 0, gMin = 255, gMax = 0, bMin = 255, bMax = 0;
        foreach (uint c in colors)
        {
            int r = (int)(c >> 16) & 0xff, g = (int)(c >> 8) & 0xff, b = (int)c & 0xff;
            if (r < rMin) rMin = r; if (r > rMax) rMax = r;
            if (g < gMin) gMin = g; if (g > gMax) gMax = g;
            if (b < bMin) bMin = b; if (b > bMax) bMax = b;
        }
        int rSpread = rMax - rMin, gSpread = gMax - gMin, bSpread = bMax - bMin;
        if (rSpread >= gSpread && rSpread >= bSpread) return (0, rSpread);
        if (gSpread >= bSpread) return (1, gSpread);
        return (2, bSpread);
    }

    private static byte GetChannel(uint color, byte channel)
    {
        return channel == 0 ? (byte)((color >> 16) & 0xff) : channel == 1 ? (byte)((color >> 8) & 0xff) : (byte)(color & 0xff);
    }

    private static Bitmap CreateIndexedBitmap(byte[] argbData, int w, int h, int stride, List<uint> palette)
    {
        int colorCount = Math.Min(palette.Count, 256);
        var result = new Bitmap(w, h, PixelFormat.Format8bppIndexed);
        var pal = result.Palette;
        for (int i = 0; i < colorCount; i++)
        {
            uint c = palette[i];
            pal.Entries[i] = Color.FromArgb((int)((c >> 24) & 0xff), (int)((c >> 16) & 0xff), (int)((c >> 8) & 0xff), (int)(c & 0xff));
        }
        for (int i = colorCount; i < 256; i++)
            pal.Entries[i] = Color.Black;
        result.Palette = pal;

        var rect = new Rectangle(0, 0, w, h);
        var bd = result.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
        try
        {
            var indices = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    int src = row + x * 4;
                    uint pixel = (uint)((argbData[src + 3] << 24) | (argbData[src + 2] << 16) | (argbData[src + 1] << 8) | argbData[src]);
                    indices[y * w + x] = (byte)FindNearestPaletteIndex(pixel, palette);
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(indices, 0, bd.Scan0, indices.Length);
        }
        finally
        {
            result.UnlockBits(bd);
        }
        return result;
    }

    private static int FindNearestPaletteIndex(uint color, List<uint> palette)
    {
        int best = 0;
        long bestDist = long.MaxValue;
        int ar = (int)((color >> 16) & 0xff), ag = (int)((color >> 8) & 0xff), ab = (int)(color & 0xff);
        for (int i = 0; i < palette.Count; i++)
        {
            uint p = palette[i];
            int pr = (int)((p >> 16) & 0xff), pg = (int)((p >> 8) & 0xff), pb = (int)(p & 0xff);
            long dr = ar - pr, dg = ag - pg, db = ab - pb;
            long d = dr * dr + dg * dg + db * db;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }
}
