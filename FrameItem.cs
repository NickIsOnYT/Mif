using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Mif;

public class FrameItem : INotifyPropertyChanged
{
    private string _delayOverrideText = "";
    private int _position;

    public string FilePath { get; }
    public BitmapSource Thumbnail { get; }
    public string FileName => Path.GetFileName(FilePath);
    public string SizeText { get; }

    /// <summary>1-based position of the frame in the sequence.</summary>
    public int Position
    {
        get => _position;
        set
        {
            if (_position == value) return;
            _position = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Per-frame delay in 1/100 sec. Empty = use default.</summary>
    public string DelayOverrideText
    {
        get => _delayOverrideText;
        set { _delayOverrideText = value ?? ""; OnPropertyChanged(); }
    }

    public FrameItem(string filePath, BitmapSource thumbnail, string sizeText)
    {
        FilePath = filePath;
        Thumbnail = thumbnail;
        SizeText = sizeText;
        _position = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
