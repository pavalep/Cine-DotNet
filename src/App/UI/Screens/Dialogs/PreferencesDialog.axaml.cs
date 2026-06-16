using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Cine.Avalonia.Managers;

namespace Cine.Avalonia.Views.Dialogs;

public partial class PreferencesDialog : Window
{
    private readonly SubtitleSettingsStore _store = new();

    public PreferencesDialog()
    {
        InitializeComponent();
        KeyDown += OnGlobalKeyDown;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // Load subtitle settings
        var defaults = _store.LoadDefaults();
        AutoLoadSubsToggle.IsChecked = defaults.AutoEnabled;
        PreferredLangInput.Text = string.Join(", ", defaults.PreferredLanguages);
        SubDirsInput.Text = string.Join(", ", defaults.ExternalSubDirectories);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // Save subtitle settings
        var langs = PreferredLangInput.Text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray() ?? new[] { "eng", "jpn", "und" };

        var dirs = SubDirsInput.Text?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToArray() ?? new[] { "./subs", "./subtitles" };

        var defaults = _store.LoadDefaults();
        _store.SaveDefaults(new SubtitleSettingsStore.SubtitleDefaults
        {
            AutoEnabled = AutoLoadSubsToggle.IsChecked ?? true,
            PreferredLanguages = langs,
            FallbackToExternal = defaults.FallbackToExternal,
            ExternalSubDirectories = dirs,
            Style = defaults.Style
        });
    }

    private void OnGlobalKeyDown(object? sender, global::Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == global::Avalonia.Input.Key.Escape)
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
