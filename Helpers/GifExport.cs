using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Mif.Helpers;

public static class GifExport
{
    private const int PropertyTagFrameDelay = 0x5100;
    private static readonly byte[] NetscapeExtension = { 0x21, 0xFF, 0x0B, 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, 0x03, 0x01, 0x00, 0x00, 0x00 };

    /// <param name="imagePaths">Ordered list of image file paths.</param>
    /// <param name="outputPath">Output GIF path.</param>
    /// <param name="delayHundredths">Default delay per frame in 1/100 second (used when delayPerFrame is null or for missing entries).</param>
    /// <param name="colorCount">Palette size 2–256; 256 = full palette.</param>
    /// <param name="firstFrameAsBackground">If true, first frame is the background layer (GIF disposal may not be set by GDI+).</param>
    /// <param name="delayPerFrame">Optional per-frame delay in 1/100 sec; if provided, length should match imagePaths.</param>
    /// <param name="loopCount">Number of times to loop (0 = infinite).</param>
    /// <param name="dontStackFrames">If true, set disposal method to clear each frame before drawing.</param>
    public static void Export(List<string> imagePaths, string outputPath, int delayHundredths,
        int colorCount = 256, bool firstFrameAsBackground = false, IReadOnlyList<int>? delayPerFrame = null,
        int loopCount = 0, bool dontStackFrames = false)
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

            if (loopCount > 0)
            {
                SetLoopCount(outputPath, loopCount);
            }

            if (dontStackFrames)
            {
                SetDisposalMethod(first, 2);
            }

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

    private static void SetLoopCount(string outputPath, int loopCount)
    {
        if (loopCount == 0) return;
        try
        {
            var gifBytes = File.ReadAllBytes(outputPath);
            var loopExt = new byte[] { 0x21, 0xFF, 0x0B, 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, 0x03, 0x01,
                (byte)(loopCount & 0xFF), (byte)((loopCount >> 8) & 0xFF), 0x00 };
            using var ms = new MemoryStream();
            ms.Write(gifBytes, 0, 13);
            ms.Write(loopExt, 0, loopExt.Length);
            ms.Write(gifBytes, 13, gifBytes.Length - 13);
            File.WriteAllBytes(outputPath, ms.ToArray());
        }
        catch { }
    }

    private static void SetDisposalMethod(Image image, int disposal)
    {
        byte[] disposalBytes = new byte[] { (byte)disposal, 0, 0, 0 };
        try
        {
            var prop = CreatePropertyItem(0x5104, 1, 4, disposalBytes);
            image.SetPropertyItem(prop);
        }
        catch { }
    }

    private static PropertyItem CreatePropertyItem(int id, short type, int length, byte[] value)
    {
#pragma warning disable SYSLIB0050
        var prop = (PropertyItem)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(PropertyItem));
#pragma warning restore SYSLIB0050
        prop.Id = id;
        prop.Type = type;
        prop.Len = length;
        prop.Value = value;
        return prop;
    }
}
