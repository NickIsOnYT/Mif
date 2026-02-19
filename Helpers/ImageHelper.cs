using System.IO;
using System.Windows.Media.Imaging;

namespace Mif.Helpers;

public static class ImageHelper
{
    public static (BitmapSource? Thumbnail, string SizeText) LoadThumbnail(string filePath, int maxWidth, int maxHeight)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        BitmapFrame frame = decoder.Frames[0];
        if (frame == null) return (null, "?");
        frame.Freeze();
        long pixelWidth = frame.PixelWidth;
        long pixelHeight = frame.PixelHeight;
        string sizeText = $"{pixelWidth} × {pixelHeight}";

        double scale = Math.Min((double)maxWidth / pixelWidth, (double)maxHeight / pixelHeight);
        if (scale >= 1.0)
            return (frame, sizeText);

        var transform = new System.Windows.Media.ScaleTransform(scale, scale);
        var thumb = new TransformedBitmap(frame, transform);
        thumb.Freeze();
        return (thumb, sizeText);
    }

    /// <summary>Load full-size bitmap for export (no thumbnail).</summary>
    public static (int Width, int Height) GetImageSize(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        return frame != null ? ((int)frame.PixelWidth, (int)frame.PixelHeight) : (0, 0);
    }
}
