namespace Mif;

public class AppSettings
{
    public string DefaultExportFolder { get; set; } = "";
    public bool UseDefaultFolderOnly { get; set; }
    public int GifColorCount { get; set; } = 256;
    public bool FirstFrameAsBackground { get; set; }
    /// <summary>Export resolution width (0 = use source size, only force even for video).</summary>
    public int ExportWidth { get; set; }
    /// <summary>Export resolution height (0 = use source size, only force even for video).</summary>
    public int ExportHeight { get; set; }
}
