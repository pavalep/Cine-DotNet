using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.Storage;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PreferencesWindow : Window
{
    private readonly SubtitleSettingsStore _subStore;
    private readonly AudioSettingsStore _audioStore = new();

    // Dirty-state tracking for subtitle settings
    private bool _originalAutoLoadSubs;
    private string _originalLangs = string.Empty;
    private string _originalDirs = string.Empty;

    public PreferencesWindow() : this(null) { }

    public PreferencesWindow(SubtitleSettingsStore? subStore)
    {
        _subStore = subStore ?? new SubtitleSettingsStore();
        InitializeComponent();
        KeyDown += OnGlobalKeyDown;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object? sender, System.EventArgs e)
    {
        var defaults = _subStore.LoadDefaults();
        AutoLoadSubsToggle.IsChecked = defaults.AutoEnabled;
        PreferredLangInput.Text = string.Join(", ", defaults.PreferredLanguages);
        SubDirsInput.Text = string.Join(", ", defaults.ExternalSubDirectories);

        _originalAutoLoadSubs = defaults.AutoEnabled;
        _originalLangs = PreferredLangInput.Text ?? string.Empty;
        _originalDirs = SubDirsInput.Text ?? string.Empty;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        var autoChanged = AutoLoadSubsToggle.IsChecked != _originalAutoLoadSubs;
        var langsChanged = (PreferredLangInput.Text ?? string.Empty) != _originalLangs;
        var dirsChanged = (SubDirsInput.Text ?? string.Empty) != _originalDirs;

        if (!autoChanged && !langsChanged && !dirsChanged)
            return;

        var langs = PreferredLangInput.Text?
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray() ?? new[] { "eng", "jpn", "und" };

        var dirs = SubDirsInput.Text?
            .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToArray() ?? new[] { "./subs", "./subtitles" };

        var defaults = _subStore.LoadDefaults();
        _subStore.SaveDefaults(new SubtitleSettingsStore.SubtitleDefaults
        {
            AutoEnabled = AutoLoadSubsToggle.IsChecked ?? true,
            PreferredLanguages = langs,
            FallbackToExternal = defaults.FallbackToExternal,
            ExternalSubDirectories = dirs,
            Style = defaults.Style
        });
    }

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnResetDefaults(object? sender, RoutedEventArgs e)
    {
        _audioStore.SaveDefaults(new AudioSettingsStore.AudioGlobalDefaults());
        _audioStore.ClearAllPerFile();

        var factoryDefaults = new SubtitleSettingsStore.SubtitleDefaults
        {
            AutoEnabled = true,
            PreferredLanguages = new[] { "eng", "jpn", "und" },
            FallbackToExternal = true,
            ExternalSubDirectories = new[] { "./subs", "./subtitles" },
            Style = new SubtitleSettingsStore.SubtitleStyle()
        };
        _subStore.SaveDefaults(factoryDefaults);

        AutoLoadSubsToggle.IsChecked = factoryDefaults.AutoEnabled;
        PreferredLangInput.Text = string.Join(", ", factoryDefaults.PreferredLanguages);
        SubDirsInput.Text = string.Join(", ", factoryDefaults.ExternalSubDirectories);

        _originalAutoLoadSubs = factoryDefaults.AutoEnabled;
        _originalLangs = PreferredLangInput.Text ?? string.Empty;
        _originalDirs = SubDirsInput.Text ?? string.Empty;
    }

    private void OnSidebarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Show/hide panels based on selected sidebar index
        PanelGeneral.IsVisible = SidebarList.SelectedIndex == 0;
        PanelAudio.IsVisible = SidebarList.SelectedIndex == 1;
        PanelSubtitles.IsVisible = SidebarList.SelectedIndex == 2;
        PanelEqualizer.IsVisible = SidebarList.SelectedIndex == 3;
        PanelAbout.IsVisible = SidebarList.SelectedIndex == 4;
    }
}
