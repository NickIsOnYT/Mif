using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace Mif.Helpers;

public static class GifExport
{
    private const int PropertyTagFrameDelay = 0x5100;

    /// <param name="imagePaths">Ordered list of image file paths.</param>
    /// <param name="outputPath">Output GIF path.</param>
    /// <param name="delayHundredths">Default delay per frame in 1/100 second (used when delayPerFrame is null or for missing entries).</param>
    /// <param name="colorCount">Palette size 2–256; 256 = full palette.</param>
    /// <param name="firstFrameAsBackground">If true, first frame is the background layer (GIF disposal may not be set by GDI+).</param>
    /// <param name="delayPerFrame">Optional per-frame delay in 1/100 sec; if provided, length should match imagePaths.</param>
    public static void Export(List<string> imagePaths, string outputPath, int delayHundredths,
        int colorCount = 256, bool firstFrameAsBackground = false, IReadOnlyList<int>? delayPerFrame = null)
    {
        if (imagePaths.Count == 0) return;
        colorCount = Math.Clamp(colorCount, 2, 256);
        int GetDelay(int index)
        {
            if (delayPerFrame != null && index < delayPerFrame.Count)
            {
                int d = delayPerFrame[index];
                return Math.Clamp(d, 1, 10000);
            }
            return delayHundredths;
        }

        Image? first = null;
        try
        {
            first = LoadAndQuantize(imagePaths[0], colorCount);
            SetFrameDelayProperty(first, GetDelay(0));

            var encoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.MimeType == "image/gif");
            var encoderParams = new EncoderParameters(2);
            encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.MultiFrame);
            encoderParams.Param[1] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);

            first.Save(outputPath, encoder, encoderParams);

            for (int i = 1; i < imagePaths.Count; i++)
            {
                encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.FrameDimensionTime);
                using var frame = LoadAndQuantize(imagePaths[i], colorCount);
                SetFrameDelayProperty(frame, GetDelay(i));
                first.SaveAdd(frame, encoderParams);
            }

            encoderParams.Param[0] = new EncoderParameter(Encoder.SaveFlag, (long)EncoderValue.Flush);
            first.SaveAdd(encoderParams);
        }
        finally
        {
            first?.Dispose();
        }
    }

    private static Image LoadAndQuantize(string path, int colorCount)
    {
        using var loaded = Image.FromFile(path);
        Bitmap? bmp = loaded as Bitmap;
        bool ownBmp = false;
        if (bmp == null) { bmp = new Bitmap(loaded); ownBmp = true; }
        try
        {
            if (colorCount >= 256)
                return (Image)bmp.Clone();
            return GifColorQuantizer.Quantize(bmp, colorCount);
        }
        finally
        {
            if (ownBmp) bmp?.Dispose();
        }
    }

    private static void SetFrameDelayProperty(Image image, int delayHundredths)
    {
        byte[] delayBytes = BitConverter.GetBytes((uint)delayHundredths);
        try
        {
            var prop = image.GetPropertyItem(PropertyTagFrameDelay);
            if (prop != null)
            {
                prop.Value = delayBytes;
                image.SetPropertyItem(prop);
            }
        }
        catch
        {
#pragma warning disable SYSLIB0050 // PropertyItem has no public constructor; this is the only way to create one
            var prop = (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PropertyItem));
#pragma warning restore SYSLIB0050
            prop.Id = PropertyTagFrameDelay;
            prop.Type = 4;
            prop.Len = 4;
            prop.Value = delayBytes;
            image.SetPropertyItem(prop);
        }
    }
}
