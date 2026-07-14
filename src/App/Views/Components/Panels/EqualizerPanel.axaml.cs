using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Layout;
using Simba.Avalonia.Services;
using Simba.Avalonia.Constants;
using Simba.Avalonia.Views.Resources;

namespace Simba.Avalonia.Views.Components.Panels;

public partial class EqualizerPanel : UserControl
{
    private IAudioManager? _manager;
    private readonly Slider[] _eqSliders = new Slider[10];
    private readonly TextBlock[] _valueLabels = new TextBlock[10];

    private static readonly string[] FreqLabels =
        { "31Hz", "62Hz", "125Hz", "250Hz", "500Hz", "1kHz", "2kHz", "4kHz", "8kHz", "16kHz" };

    public EqualizerPanel()
    {
        InitializeComponent();
        BuildSliders();
        WireEvents();
    }

    /// <summary>
    /// Sets the audio manager and loads its current equalizer state.
    /// Call this once after the panel is placed in the visual tree.
    /// </summary>
    public void SetAudioManager(IAudioManager manager)
    {
        _manager = manager;
        LoadFromManager();
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

        float delay = Math.Clamp(_manager.AudioDelay, -10, 10);
        DelaySlider.Value = delay;
        DelayNumeric.Value = (decimal)delay;
        DelayLabel.Text = $"{delay:F2}s";
    }

    private static object? SafeResource(object key)
    {
        var app = Application.Current;
        if (app?.Styles != null && app.Styles.TryGetResource(key, app.ActualThemeVariant, out var r))
            return r;
        return null;
    }

    private void BuildSliders()
    {
        SlidersPanel.Children.Clear();

        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(4, 0),
                Width = 36
            };

            var freqLabel = new TextBlock
            {
                Text = FreqLabels[i],
                FontSize = Token.Size("font-size-caption"),
                Foreground = (IBrush?)(SafeResource("OsdForeground") ?? global::Avalonia.Media.Brushes.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.5,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var slider = new Slider
            {
                Minimum = -20,
                Maximum = 20,
                TickFrequency = 10,
                Width = 32,
                Height = 130,
                Orientation = Orientation.Vertical,
                Foreground = (IBrush?)(SafeResource("AccentColor") ?? global::Avalonia.Media.Brushes.White),
                Value = 0
            };
            slider.SetValue(global::Avalonia.Automation.AutomationProperties.HelpTextProperty, FreqLabels[i] + " equalizer band");
            slider.Classes.Add("compact");
            slider.TabIndex = 14 + idx;

            var valueLabel = new TextBlock
            {
                Text = "0",
                FontSize = Token.Size("font-size-caption"),
                Foreground = (IBrush?)(SafeResource("OsdForeground") ?? global::Avalonia.Media.Brushes.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                Opacity = 0.7,
                Margin = new Thickness(0, 4, 0, 0)
            };

            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                {
                    var val = Math.Round(slider.Value, 1);
                    valueLabel.Text = val >= 0 ? $"+{val:F1}" : $"{val:F1}";
                    _manager?.SetEqualizerBand(idx, val);
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
        DelayNumeric.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(NumericUpDown.Value))
            {
                DelaySlider.Value = (double)(DelayNumeric.Value ?? 0);
                DelayLabel.Text = $"{DelayNumeric.Value:F2}s";
            }
        };
    }

    private void OnPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string presetName || _manager == null) return;

        _manager.ApplyEqualizerPreset(presetName);

        // Update selected visual state on preset buttons
        foreach (var child in PresetWrapPanel.Children)
        {
            if (child is Button presetBtn)
            {
                presetBtn.Classes.Remove("selected");
            }
        }
        btn.Classes.Add("selected");

        var bands = _manager.EqualizerBands;
        for (int i = 0; i < 10 && i < bands.Length; i++)
        {
            _eqSliders[i].Value = Math.Clamp(bands[i], -20, 20);
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _manager?.ResetAllAudio();
        LoadFromManager();
    }
}
