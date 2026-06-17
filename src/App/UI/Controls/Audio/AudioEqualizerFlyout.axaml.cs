using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Managers;

namespace Cine.Avalonia.Controls;

public partial class AudioEqualizerFlyout : UserControl
{
    private readonly AudioManager? _manager;
    private readonly Slider[] _eqSliders = new Slider[10];
    private readonly TextBlock[] _valueLabels = new TextBlock[10];

    /// <summary>Set by the owner to allow the close button to dismiss the flyout.</summary>
    public Action? CloseAction { get; set; }

    private static readonly string[] FreqLabels = { "31Hz", "62Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz" };

    private static IBrush SafeResource(string key, IBrush fallback)
    {
        // Resources may not be available when the flyout is created outside the visual tree
        try
        {
            if (global::Avalonia.Application.Current?.TryFindResource(key, out var result) == true && result is IBrush brush)
                return brush;
        }
        catch { }
        return fallback;
    }

    public AudioEqualizerFlyout()
    {
        InitializeComponent();
        BuildSliders();
        WireEvents();
    }

    public AudioEqualizerFlyout(AudioManager manager) : this()
    {
        _manager = manager;
        LoadFromManager();
    }

    private void BuildSliders()
    {
        SlidersPanel.Children.Clear();

        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            var stack = new StackPanel
            {
                Orientation = global::Avalonia.Layout.Orientation.Vertical,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
                Margin = new Thickness(3, 0),
                Width = 36
            };

            var freqLabel = new TextBlock
            {
                Text = FreqLabels[i],
                FontSize = 9,
                Foreground = SafeResource("OsdForeground", global::Avalonia.Media.Brushes.White),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Opacity = 0.5,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var slider = new Slider
            {
                Minimum = -20,
                Maximum = 20,
                TickFrequency = 10,
                Width = 32,
                Height = 130,
                Orientation = global::Avalonia.Layout.Orientation.Vertical,
                Foreground = SafeResource("AccentColor", global::Avalonia.Media.Brushes.White),
                Value = 0
            };

            var valueLabel = new TextBlock
            {
                Text = "0",
                FontSize = 10,
                Foreground = SafeResource("OsdForeground", global::Avalonia.Media.Brushes.White),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
                Opacity = 0.8,
                Margin = new Thickness(0, 4, 0, 0)
            };

            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                {
                    var val = Math.Round(slider.Value, 1);
                    valueLabel.Text = val >= 0 ? $"+{val:F1}" : $"{val:F1}";
                    if (_manager != null)
                    {
                        _manager.SetEqualizerBand(idx, val);
                    }
                }
            };

            _eqSliders[i] = slider;
            _valueLabels[i] = valueLabel;

            stack.Children.Add(freqLabel);
            stack.Children.Add(slider);
            stack.Children.Add(valueLabel);
            SlidersPanel.Children.Add(stack);
        }
    }

    private void WireEvents()
    {
        if (CloseBtn != null)
            CloseBtn.Click += (_, _) => CloseAction?.Invoke();

        NormToggle.IsCheckedChanged += (_, _) =>
        {
            if (_manager != null)
                _manager.IsAudioNormalizationEnabled = NormToggle.IsChecked ?? false;
        };
        DialogueToggle.IsCheckedChanged += (_, _) =>
        {
            if (_manager != null)
                _manager.IsDialogueBoostEnabled = DialogueToggle.IsChecked ?? false;
        };
        DelaySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Slider.Value))
            {
                DelayLabel.Text = $"{DelaySlider.Value:F2}s";
            }
        };
    }

    public void LoadFromManager()
    {
        if (_manager == null) return;

        var bands = _manager.EqualizerBands;
        for (int i = 0; i < 10 && i < bands.Length; i++)
        {
            _eqSliders[i].Value = Math.Clamp(bands[i], -20, 20);
        }

        NormToggle.IsChecked = _manager.IsAudioNormalizationEnabled;
        DialogueToggle.IsChecked = _manager.IsDialogueBoostEnabled;

        DelaySlider.Value = Math.Clamp(_manager.AudioDelay, -10, 10);
        DelayLabel.Text = $"{_manager.AudioDelay:F2}s";
    }

    private void OnPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not global::Avalonia.Controls.Button btn || btn.Tag is not string presetName || _manager == null) return;

        _manager.ApplyEqualizerPreset(presetName);

        var bands = _manager.EqualizerBands;
        for (int i = 0; i < 10 && i < bands.Length; i++)
        {
            _eqSliders[i].Value = Math.Clamp(bands[i], -20, 20);
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _manager?.ResetAllAudio();
        if (_manager != null)
        {
            LoadFromManager();
        }
    }
}
