using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaLayout = Avalonia.Layout;
using Cine.Avalonia.ViewModels;
using Color = Avalonia.Media.Color;

namespace Cine.Avalonia.Views.Dialogs;

public partial class EqualizerDialog : Window
{
    private readonly MainViewModel? _vm;
    private readonly Slider[] _sliders = new Slider[10];

    private static readonly string[] _freqLabels = { "31", "62", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" };

    public EqualizerDialog()
    {
        InitializeComponent();
    }

    public EqualizerDialog(MainViewModel vm) : this()
    {
        _vm = vm;
        BuildSliders();
        LoadCurrentValues();
    }

    private void BuildSliders()
    {
        for (int i = 0; i < 10; i++)
        {
            var idx = i;
            var stack = new StackPanel
            {
                Orientation = AvaloniaLayout.Orientation.Vertical,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
                VerticalAlignment = AvaloniaLayout.VerticalAlignment.Stretch,
                Margin = new Thickness(2, 0)
            };

            // Frequency label (top)
            stack.Children.Add(new TextBlock
            {
                Text = _freqLabels[idx],
                FontSize = 10,
                Foreground = AppColors.TextOnDarkSecondary,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // Vertical slider
            var slider = new Slider
            {
                Minimum = -20,
                Maximum = 20,
                TickFrequency = 5,
                Orientation = AvaloniaLayout.Orientation.Vertical,
                Height = 200,
                Width = 28,
                Value = 0,
                IsSnapToTickEnabled = false,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center
            };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                    _vm?.SetEqualizerBand(idx, slider.Value);
            };
            _sliders[idx] = slider;
            stack.Children.Add(slider);

            // Value label (bottom)
            var valLabel = new TextBlock
            {
                Text = "0",
                FontSize = 9,
                Foreground = AppColors.TextOnDarkHint,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name == nameof(Slider.Value))
                    valLabel.Text = $"{slider.Value:F0}";
            };
            stack.Children.Add(valLabel);

            SlidersStack.Children.Add(stack);
        }
    }

    private void LoadCurrentValues()
    {
        if (_vm == null) return;
        var bands = _vm.EqualizerBands;
        for (int i = 0; i < 10 && i < bands.Length; i++)
            _sliders[i].Value = bands[i];
        PresetLabel.Text = _vm.EqualizerPresetName;
    }

    private void OnPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is global::Avalonia.Controls.Button btn && btn.Tag is string presetName)
        {
            _vm?.ApplyEqualizerPreset(presetName);
            LoadCurrentValues();
            PresetLabel.Text = presetName;
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
