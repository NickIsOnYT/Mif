using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using Mif.Helpers;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Mif;

public partial class MainWindow
{
    private readonly ObservableCollection<FrameItem> _frames = new();
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        FrameList.ItemsSource = _frames;
        _settings = SettingsManager.Load();
        LoadWindowIcon();
    }

    private void LoadWindowIcon()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) + ".exe";
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon == null) return;
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch { /* ignore */ }
    }

    private void UpdateEmptyState()
    {
        bool hasFrames = _frames.Count > 0;
        TxtNoFrames.Visibility = hasFrames ? Visibility.Collapsed : Visibility.Visible;
        FrameList.Visibility = hasFrames ? Visibility.Visible : Visibility.Collapsed;
        BtnRemoveFrame.IsEnabled = hasFrames;
        BtnClear.IsEnabled = hasFrames;
        BtnExportGif.IsEnabled = hasFrames;
        BtnExportVideo.IsEnabled = hasFrames;
        RenumberFrames();
    }

    private void RenumberFrames()
    {
        for (int i = 0; i < _frames.Count; i++)
        {
            _frames[i].Position = i + 1;
        }
    }

    private static IEnumerable<string> SortFilesNaturally(IEnumerable<string> paths)
    {
        return paths
            .Select(p =>
            {
                string name = Path.GetFileNameWithoutExtension(p);
                int i = name.Length - 1;
                while (i >= 0 && char.IsDigit(name[i])) i--;
                string prefix = name.Substring(0, i + 1);
                int number = int.MaxValue;
                if (i < name.Length - 1)
                {
                    string numStr = name[(i + 1)..];
                    if (!int.TryParse(numStr, out number)) number = int.MaxValue;
                }
                return new { Path = p, Prefix = prefix, Number = number, Name = name };
            })
            .OrderBy(x => x.Prefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Path);
    }

    private void AdvancedSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AdvancedSettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _settings = dlg.Settings;
            SettingsManager.Save(_settings);
        }
    }

    private void ImportFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder containing images",
            UseDescriptionForTitle = true
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" };
        var files = SortFilesNaturally(
                Directory.GetFiles(dlg.SelectedPath)
                    .Where(p => imageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant())))
            .ToArray();

        if (files.Length == 0)
        {
            MessageBox.Show("No image files found in that folder.", "Import folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (string path in files)
        {
            try
            {
                var (thumbnail, sizeText) = ImageHelper.LoadThumbnail(path, 80, 60);
                if (thumbnail != null)
                    _frames.Add(new FrameItem(path, thumbnail, sizeText));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load: {path}\n\n{ex.Message}", "Load error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        RenumberFrames();
        UpdateEmptyState();
        MessageBox.Show($"Imported {files.Length} image(s).", "Import folder", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnAddFrames_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff;*.tif|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Select images to add as frames"
        };
        if (dlg.ShowDialog() != true) return;

        AddFramesFromPaths(dlg.FileNames);
    }

    private void BtnRemoveFrame_Click(object sender, RoutedEventArgs e)
    {
        if (FrameList.SelectedItem is FrameItem item)
        {
            _frames.Remove(item);
            RenumberFrames();
            UpdateEmptyState();
        }
        else
            MessageBox.Show("Select a frame to remove.", "Remove", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnRemoveThisFrame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FrameItem item)
        {
            _frames.Remove(item);
            RenumberFrames();
            UpdateEmptyState();
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("Clear all frames?", "Clear", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _frames.Clear();
            UpdateEmptyState();
        }
    }

    private void FrameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BtnRemoveFrame.IsEnabled = _frames.Count > 0 && FrameList.SelectedItem != null;
    }

    private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not FrameItem item) return;
        int idx = _frames.IndexOf(item);
        if (idx <= 0) return;
        _frames.Move(idx, idx - 1);
        RenumberFrames();
    }

    private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not FrameItem item) return;
        int idx = _frames.IndexOf(item);
        if (idx < 0 || idx >= _frames.Count - 1) return;
        _frames.Move(idx, idx + 1);
        RenumberFrames();
    }

    private void ChangeFramePosition(FrameItem item, int newPosition)
    {
        if (_frames.Count == 0) return;

        int clamped = Math.Max(1, Math.Min(newPosition, _frames.Count));
        int oldIndex = _frames.IndexOf(item);
        int targetIndex = clamped - 1;

        if (oldIndex < 0)
            return;

        if (oldIndex != targetIndex)
        {
            _frames.Move(oldIndex, targetIndex);
        }

        RenumberFrames();
        FrameList.SelectedItem = item;
        FrameList.ScrollIntoView(item);
    }

    private void FramePosition_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb || tb.Tag is not FrameItem item) return;

        string text = tb.Text.Trim();
        if (!int.TryParse(text, out int newPos))
        {
            tb.Text = item.Position.ToString();
            return;
        }

        ChangeFramePosition(item, newPos);
        tb.Text = item.Position.ToString();
    }

    private void FramePosition_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        e.Handled = true;
        FramePosition_LostFocus(sender, e);
    }

    private void AddFramesFromPaths(IEnumerable<string> paths)
    {
        foreach (string path in SortFilesNaturally(paths))
        {
            try
            {
                var (thumbnail, sizeText) = ImageHelper.LoadThumbnail(path, 80, 60);
                if (thumbnail != null)
                    _frames.Add(new FrameItem(path, thumbnail, sizeText));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load image: {path}\n\n{ex.Message}", "Load error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        RenumberFrames();
        UpdateEmptyState();
    }

    private void FrameArea_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Copy;
        else
            e.Effects = System.Windows.DragDropEffects.None;

        e.Handled = true;
    }

    private void FrameArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            e.Effects = System.Windows.DragDropEffects.Copy;
        else
            e.Effects = System.Windows.DragDropEffects.None;

        e.Handled = true;
    }

    private void FrameArea_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            return;

        var dropped = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[] ?? Array.Empty<string>();
        if (dropped.Length == 0)
            return;

        string[] imageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".tif" };
        var paths = new List<string>();

        foreach (string path in dropped)
        {
            try
            {
                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (imageExtensions.Contains(ext))
                        paths.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    var files = Directory.GetFiles(path)
                        .Where(p => imageExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()));
                    paths.AddRange(files);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read from: {path}\n\n{ex.Message}", "Drop error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        if (paths.Count == 0)
            return;

        AddFramesFromPaths(paths);
    }

    private bool TryGetFrameDelay(out int delayHundredths)
    {
        delayHundredths = 0;
        if (!int.TryParse(TxtFrameDelay.Text.Trim(), out int val) || val < 1 || val > 10000)
        {
            MessageBox.Show("Frame delay must be a number between 1 and 10000 (1/100 sec).", "Invalid delay", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        delayHundredths = val;
        return true;
    }

    private string? GetExportPath(string ext, bool skipDialog)
    {
        string defaultDir = _settings.DefaultExportFolder?.Trim() ?? "";
        if (skipDialog && !string.IsNullOrEmpty(defaultDir) && Directory.Exists(defaultDir))
        {
            string name = $"MifExport_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.{ext}";
            return Path.Combine(defaultDir, name);
        }

        var dlg = new SaveFileDialog
        {
            Filter = ext == "gif" ? "GIF (*.gif)|*.gif|All files (*.*)|*.*" : "MP4 (*.mp4)|*.mp4|All files (*.*)|*.*",
            DefaultExt = ext,
            Title = ext == "gif" ? "Export as GIF" : "Export as video",
            InitialDirectory = !string.IsNullOrEmpty(defaultDir) && Directory.Exists(defaultDir) ? defaultDir : null
        };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    private List<int> GetPerFrameDelays(int defaultDelay)
    {
        var list = new List<int>(_frames.Count);
        foreach (var f in _frames)
        {
            if (int.TryParse(f.DelayOverrideText?.Trim(), out int d) && d >= 1 && d <= 10000)
                list.Add(d);
            else
                list.Add(defaultDelay);
        }
        return list;
    }

    private void BtnExportGif_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0) { MessageBox.Show("Add at least one frame.", "Export", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!TryGetFrameDelay(out int delayHundredths)) return;

        string? path = GetExportPath("gif", _settings.UseDefaultFolderOnly);
        if (path == null) return;

        try
        {
            var paths = _frames.Select(f => f.FilePath).ToList();
            var delays = GetPerFrameDelays(delayHundredths);
            GifExport.Export(paths, path, delayHundredths,
                _settings.GifColorCount, _settings.FirstFrameAsBackground, delays,
                _settings.GifLoopCount, _settings.DontStackFrames);
            MessageBox.Show($"GIF saved to:\n{path}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Export error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnExportVideo_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0) { MessageBox.Show("Add at least one frame.", "Export", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (!TryGetFrameDelay(out int delayHundredths)) return;

        string? path = GetExportPath("mp4", _settings.UseDefaultFolderOnly);
        if (path == null) return;

        try
        {
            VideoExport.Export(_frames.Select(f => f.FilePath).ToList(), path, delayHundredths,
                _settings.ExportWidth, _settings.ExportHeight);
            MessageBox.Show($"Video saved to:\n{path}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed:\n{ex.Message}", "Export error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
