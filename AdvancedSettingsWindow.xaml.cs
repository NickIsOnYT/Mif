using System.Windows;

namespace Mif;

public partial class AdvancedSettingsWindow
{
    public AppSettings Settings { get; private set; }

    public AdvancedSettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Settings = new AppSettings
        {
            DefaultExportFolder = current.DefaultExportFolder,
            UseDefaultFolderOnly = current.UseDefaultFolderOnly,
            GifColorCount = current.GifColorCount,
            FirstFrameAsBackground = current.FirstFrameAsBackground,
            ExportWidth = current.ExportWidth,
            ExportHeight = current.ExportHeight
        };
        TxtDefaultFolder.Text = Settings.DefaultExportFolder;
        ChkUseDefaultFolderOnly.IsChecked = Settings.UseDefaultFolderOnly;
        ChkFirstFrameAsBackground.IsChecked = Settings.FirstFrameAsBackground;
        TxtExportWidth.Text = Settings.ExportWidth > 0 ? Settings.ExportWidth.ToString() : "";
        TxtExportHeight.Text = Settings.ExportHeight > 0 ? Settings.ExportHeight.ToString() : "";
        SelectGifColorCombo(Settings.GifColorCount);
    }

    private void SelectGifColorCombo(int count)
    {
        foreach (var item in CmbGifColors.Items)
            if (item is System.Windows.Controls.ComboBoxItem cbi && cbi.Tag is string s && int.TryParse(s, out int n) && n == count)
            {
                CmbGifColors.SelectedItem = item;
                return;
            }
        CmbGifColors.SelectedIndex = CmbGifColors.Items.Count - 1;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select default export folder",
            UseDescriptionForTitle = true,
            SelectedPath = TxtDefaultFolder.Text
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtDefaultFolder.Text = dlg.SelectedPath;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Settings.DefaultExportFolder = TxtDefaultFolder.Text?.Trim() ?? "";
        Settings.UseDefaultFolderOnly = ChkUseDefaultFolderOnly.IsChecked == true;
        Settings.FirstFrameAsBackground = ChkFirstFrameAsBackground.IsChecked == true;
        int.TryParse(TxtExportWidth.Text?.Trim(), out int w);
        int.TryParse(TxtExportHeight.Text?.Trim(), out int h);
        Settings.ExportWidth = Math.Max(0, w);
        Settings.ExportHeight = Math.Max(0, h);
        if (CmbGifColors.SelectedItem is System.Windows.Controls.ComboBoxItem cbi && cbi.Tag is string s && int.TryParse(s, out int n))
            Settings.GifColorCount = Math.Clamp(n, 2, 256);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
