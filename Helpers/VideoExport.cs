using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace Mif.Helpers;

public static class VideoExport
{
    /// <param name="imagePaths">Ordered list of image file paths.</param>
    /// <param name="outputPath">Output video path (e.g. .mp4).</param>
    /// <param name="delayHundredths">Delay per frame in 1/100 second (used for framerate).</param>
    /// <param name="exportWidth">Optional export width (must be even). 0 = keep source, only force even.</param>
    /// <param name="exportHeight">Optional export height (must be even). 0 = keep source, only force even.</param>
    public static void Export(List<string> imagePaths, string outputPath, int delayHundredths,
        int exportWidth = 0, int exportHeight = 0)
    {
        double delaySec = delayHundredths / 100.0;
        double fps = 1.0 / delaySec;

        string? ffmpeg = FindFfmpeg();
        if (string.IsNullOrEmpty(ffmpeg))
            throw new InvalidOperationException(
                "FFmpeg was not found. Install FFmpeg and add it to your PATH, or place ffmpeg.exe in the same folder as this app.");

        string tempDir = Path.Combine(Path.GetTempPath(), "Mif_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            for (int i = 0; i < imagePaths.Count; i++)
            {
                string src = imagePaths[i];
                string ext = Path.GetExtension(src).ToLowerInvariant();
                string dest = Path.Combine(tempDir, $"frame_{(i + 1):D4}.png");
                if (ext is ".png" or ".bmp" or ".tiff" or ".tif")
                {
                    File.Copy(src, dest, overwrite: true);
                }
                else
                {
                    using var img = Image.FromFile(src);
                    img.Save(dest, System.Drawing.Imaging.ImageFormat.Png);
                }
            }

            // H.264 requires width and height divisible by 2. Scale filter: use custom size or force even.
            int w = exportWidth > 0 ? (exportWidth & ~1) : 0;
            int h = exportHeight > 0 ? (exportHeight & ~1) : 0;
            string vf = (w > 0 && h > 0)
                ? $"-vf \"scale={w}:{h}\""
                : "-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\"";

            string args = $"-y -framerate {fps:R} -i \"{Path.Combine(tempDir, "frame_%04d.png")}\" {vf} -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(startInfo);
            if (process == null)
                throw new InvalidOperationException("Failed to start FFmpeg.");
            string err = process.StandardError.ReadToEnd();
            process.WaitForExit(120000); // 2 min
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"FFmpeg failed (exit {process.ExitCode}).\n{err}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch { /* ignore */ }
        }
    }

    private static string? FindFfmpeg()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
        {
            foreach (string dir in path.Split(Path.PathSeparator))
            {
                string exe = Path.Combine(dir.Trim(), "ffmpeg.exe");
                if (File.Exists(exe)) return exe;
            }
        }
        string appDir = AppContext.BaseDirectory;
        string local = Path.Combine(appDir, "ffmpeg.exe");
        if (File.Exists(local)) return local;
        return null;
    }
}
