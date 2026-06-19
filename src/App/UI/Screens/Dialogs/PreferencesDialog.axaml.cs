using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Cine.Avalonia.Managers;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly SubtitleSettingsStore _subStore = new();
    private readonly AudioSettingsStore _audioStore = new();

    // Dirty-state tracking — only save if values changed
    private bool _originalAutoLoadSubs;
    private string _originalLangs = string.Empty;
    private string _originalDirs = string.Empty;

    public PreferencesDialog()
    {
        InitializeComponent();
        KeyDown += OnGlobalKeyDown;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        var defaults = _subStore.LoadDefaults();
        AutoLoadSubsToggle.IsChecked = defaults.AutoEnabled;
        PreferredLangInput.Text = string.Join(", ", defaults.PreferredLanguages);
        SubDirsInput.Text = string.Join(", ", defaults.ExternalSubDirectories);

        // Snapshot for dirty-state tracking
        _originalAutoLoadSubs = defaults.AutoEnabled;
        _originalLangs = PreferredLangInput.Text ?? string.Empty;
        _originalDirs = SubDirsInput.Text ?? string.Empty;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Check if anything actually changed
        var autoChanged = AutoLoadSubsToggle.IsChecked != _originalAutoLoadSubs;
        var langsChanged = (PreferredLangInput.Text ?? string.Empty) != _originalLangs;
        var dirsChanged = (SubDirsInput.Text ?? string.Empty) != _originalDirs;

        if (!autoChanged && !langsChanged && !dirsChanged)
            return; // No changes — skip save

        var langs = PreferredLangInput.Text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray() ?? new[] { "eng", "jpn", "und" };

        var dirs = SubDirsInput.Text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
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

    private void OnViewAllShortcutsClick(object? sender, RoutedEventArgs e)
    {
        var dialog = new KeyboardShortcutsDialog();
        dialog.ShowDialog(this);
    }
}
