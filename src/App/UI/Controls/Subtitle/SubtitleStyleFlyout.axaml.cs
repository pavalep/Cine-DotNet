using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Cine.Avalonia.Services;

namespace Cine.Avalonia.Controls.Subtitle;

/// <summary>
/// Flyout content for subtitle style controls (font size, position, delay, track, visibility).
/// Should be hosted inside a Popup or Flyout. Binds to ISubtitleManager directly.
/// </summary>
public partial class SubtitleStyleFlyout : UserControl
{
    private ISubtitleManager? _manager;
    private bool _isUpdating;

    private static readonly string[] CommonFonts = new[]
    {
        "Arial", "Segoe UI", "Tahoma", "Verdana", "Times New Roman",
        "Courier New", "Georgia", "Trebuchet MS", "Impact", "Comic Sans MS"
    };

    public SubtitleStyleFlyout()
    {
        InitializeComponent();

        // Populate font combo
        FontCombo.ItemsSource = CommonFonts;

        // Wire event handlers
        SubtitleToggle.IsCheckedChanged += OnToggleChanged;
        TrackCombo.SelectionChanged += OnTrackSelectionChanged;
        FontSizeSlider.ValueChanged += OnFontSizeChanged;
        PositionSlider.ValueChanged += OnPositionChanged;
        DelaySlider.ValueChanged += OnDelaySliderChanged;
        DelayMinusBtn.Click += (_, _) => NudgeDelay(-0.5);
        DelayPlusBtn.Click += (_, _) => NudgeDelay(0.5);
        DelayResetBtn.Click += (_, _) => ResetDelay();
        FontCombo.SelectionChanged += OnFontSelectionChanged;
        BorderSlider.ValueChanged += OnBorderChanged;
        ShadowSlider.ValueChanged += OnShadowChanged;
        ColorInput.TextChanged += OnColorTextChanged;
        CloseBtn.Click += OnClose;
        AddTrackBtn.Click += OnAddTrack;
        ResetAllBtn.Click += OnResetAll;
    }

    /// <summary>
    /// Bind this flyout to a ISubtitleManager.
    /// </summary>
    public void Bind(ISubtitleManager manager)
    {
        _manager = manager;

        // Sync initial values
        _isUpdating = true;

        SubtitleToggle.IsChecked = manager.IsSubtitleEnabled;
        FontSizeSlider.Value = manager.SubtitleFontScale;
        PositionSlider.Value = manager.SubtitlePosition;
        DelaySlider.Value = manager.SubtitleDelay;
        BorderSlider.Value = manager.SubtitleBorderSize;
        ShadowSlider.Value = manager.SubtitleShadowOffset;
        FontCombo.SelectedItem = manager.SubtitleFont;
        ColorInput.Text = manager.SubtitleColor;
        UpdateColorPreview(manager.SubtitleColor);

        UpdateTrackCombo();
        UpdateLabels();
        UpdateStyleControlsEnabled(manager.HasTextSubtitles);

        _isUpdating = false;

        // Subscribe to manager changes
        manager.PropertyChanged += OnManagerPropertyChanged;
    }

    /// <summary>Unbind when closing.</summary>
    public void Unbind()
    {
        if (_manager != null)
            _manager.PropertyChanged -= OnManagerPropertyChanged;
        _manager = null;
    }

    private void OnManagerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_manager == null || _isUpdating) return;

        _isUpdating = true;
        switch (e.PropertyName)
        {
            case nameof(ISubtitleManager.IsSubtitleEnabled):
                SubtitleToggle.IsChecked = _manager.IsSubtitleEnabled;
                break;
            case nameof(ISubtitleManager.SubtitleFontScale):
                FontSizeSlider.Value = _manager.SubtitleFontScale;
                break;
            case nameof(ISubtitleManager.SubtitlePosition):
                PositionSlider.Value = _manager.SubtitlePosition;
                break;
            case nameof(ISubtitleManager.SubtitleDelay):
                DelaySlider.Value = _manager.SubtitleDelay;
                break;
            case nameof(ISubtitleManager.SubtitleBorderSize):
                BorderSlider.Value = _manager.SubtitleBorderSize;
                break;
            case nameof(ISubtitleManager.SubtitleShadowOffset):
                ShadowSlider.Value = _manager.SubtitleShadowOffset;
                break;
            case nameof(ISubtitleManager.SubtitleFont):
                FontCombo.SelectedItem = _manager.SubtitleFont;
                break;
            case nameof(ISubtitleManager.SubtitleColor):
                ColorInput.Text = _manager.SubtitleColor;
                UpdateColorPreview(_manager.SubtitleColor);
                break;
            case nameof(ISubtitleManager.HasTextSubtitles):
                UpdateStyleControlsEnabled(_manager.HasTextSubtitles);
                break;
        }
        UpdateLabels();
        _isUpdating = false;
    }

    private void UpdateTrackCombo()
    {
        if (_manager == null) return;
        var items = _manager.SubtitleTracks
            .Where(t => !t.IsPseudoEntry)
            .Select(t => t.DisplayName)
            .ToArray();
        TrackCombo.ItemsSource = items;
        TrackCombo.IsVisible = items.Length > 0;
        TrackCombo.PlaceholderText = items.Length > 0 ? "Select track" : "No subtitles";
    }

    private void UpdateLabels()
    {
        if (_manager == null) return;
        FontSizeValue.Text = $"{_manager.SubtitleFontScale:F1}×";
        PositionValue.Text = $"{_manager.SubtitlePosition}%";
        DelayValue.Text = $"{_manager.SubtitleDelay:F1}s";
        BorderValue.Text = $"{_manager.SubtitleBorderSize:F1}";
        ShadowValue.Text = $"{_manager.SubtitleShadowOffset:F1}";
    }

    /// <summary>Enable/disable style controls based on whether the current track is text-based.</summary>
    private void UpdateStyleControlsEnabled(bool hasText)
    {
        FontSizeSlider.IsEnabled = hasText;
        PositionSlider.IsEnabled = hasText;
        FontCombo.IsEnabled = hasText;
        BorderSlider.IsEnabled = hasText;
        ShadowSlider.IsEnabled = hasText;
        ColorInput.IsEnabled = hasText;
        // Delay works with both text and bitmap subtitles
        global::Avalonia.Controls.ToolTip.SetTip(FontSizeSlider, hasText ? "Adjust font size" : "Not available for bitmap subtitles (PGS/VOBSUB)");
        global::Avalonia.Controls.ToolTip.SetTip(PositionSlider, hasText ? "Adjust vertical position" : "Not available for bitmap subtitles (PGS/VOBSUB)");
    }

    private void UpdateColorPreview(string hex)
    {
        try
        {
            if (global::Avalonia.Media.Color.TryParse(hex, out var color))
                ColorPreview.Background = new global::Avalonia.Media.SolidColorBrush(color);
        }
        catch { /* invalid hex — keep current */ }
    }

    // ── Event Handlers ──

    private void OnToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.IsSubtitleEnabled = SubtitleToggle.IsChecked == true;
    }

    private void OnTrackSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        var tracks = _manager.SubtitleTracks.Where(t => !t.IsPseudoEntry).ToList();
        var selectedIndex = TrackCombo.SelectedIndex;
        if (selectedIndex >= 0 && selectedIndex < tracks.Count)
            tracks[selectedIndex].SelectCommand.Execute(tracks[selectedIndex]);
    }

    private void OnFontSizeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.SubtitleFontScale = Math.Round(e.NewValue, 1);
        FontSizeValue.Text = $"{_manager.SubtitleFontScale:F1}×";
    }

    private void OnPositionChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.SubtitlePosition = (int)Math.Round(e.NewValue);
        PositionValue.Text = $"{_manager.SubtitlePosition}%";
    }

    private void OnDelaySliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.SubtitleDelay = (float)Math.Round(e.NewValue, 1);
        DelayValue.Text = $"{_manager.SubtitleDelay:F1}s";
    }

    private void NudgeDelay(double delta)
    {
        if (_manager == null) return;
        var current = _manager.SubtitleDelay;
        _manager.SubtitleDelay = (float)Math.Clamp(current + delta, -10, 10);
        _isUpdating = true;
        DelaySlider.Value = _manager.SubtitleDelay;
        DelayValue.Text = $"{_manager.SubtitleDelay:F1}s";
        _isUpdating = false;
    }

    private void ResetDelay()
    {
        if (_manager == null) return;
        _manager.SubtitleDelay = 0;
        _isUpdating = true;
        DelaySlider.Value = 0;
        DelayValue.Text = "0.0s";
        _isUpdating = false;
    }

    private void OnFontSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating || _manager == null || FontCombo.SelectedItem is not string font) return;
        _manager.SubtitleFont = font;
    }

    private void OnBorderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.SubtitleBorderSize = Math.Round(e.NewValue, 1);
        BorderValue.Text = $"{_manager.SubtitleBorderSize:F1}";
    }

    private void OnShadowChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        _manager.SubtitleShadowOffset = Math.Round(e.NewValue, 1);
        ShadowValue.Text = $"{_manager.SubtitleShadowOffset:F1}";
    }

    private void OnColorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isUpdating || _manager == null) return;
        var text = ColorInput.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        // Only apply valid hex colors
        if (text.StartsWith("#") && (text.Length == 4 || text.Length == 7 || text.Length == 9))
        {
            try
            {
                if (global::Avalonia.Media.Color.TryParse(text, out _))
                {
                    _manager.SubtitleColor = text;
                    UpdateColorPreview(text);
                }
            }
            catch { }
        }
    }

    private void OnResetAll(object? sender, RoutedEventArgs e)
    {
        _manager?.ResetAllSubtitles();
        _isUpdating = true;
        if (_manager != null)
        {
            FontSizeSlider.Value = _manager.SubtitleFontScale;
            PositionSlider.Value = _manager.SubtitlePosition;
            DelaySlider.Value = _manager.SubtitleDelay;
            BorderSlider.Value = _manager.SubtitleBorderSize;
            ShadowSlider.Value = _manager.SubtitleShadowOffset;
            FontCombo.SelectedItem = _manager.SubtitleFont;
            ColorInput.Text = _manager.SubtitleColor;
            UpdateColorPreview(_manager.SubtitleColor);
            SubtitleToggle.IsChecked = _manager.IsSubtitleEnabled;
        }
        UpdateLabels();
        _isUpdating = false;
    }

    /// <summary>Optional callback to close the parent flyout.</summary>
    public Action? CloseAction { get; set; }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        CloseAction?.Invoke();
    }

    private async void OnAddTrack(object? sender, RoutedEventArgs e)
    {
        if (_manager == null) return;
        try
        {
            await _manager.AddSubtitleTrackAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            global::Cine.Core.Log.ForContext<SubtitleStyleFlyout>().Error(ex, "Add subtitle track failed");
        }
    }
}
