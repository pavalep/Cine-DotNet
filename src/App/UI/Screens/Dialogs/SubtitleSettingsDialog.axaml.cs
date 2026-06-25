using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Managers;
using Cine.Avalonia.Services;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;
using AvaloniaLayout = Avalonia.Layout;

namespace Cine.Avalonia.Views.Dialogs;

public partial class SubtitleSettingsDialog : Window
{
    private readonly ISubtitleManager _mgr;

    private static IBrush? GetThemeBrush(string key)
        => global::Avalonia.Application.Current?.FindResource(key) as IBrush;

    private static readonly string[] CommonFonts =
    {
        "Arial", "Calibri", "Segoe UI", "Tahoma", "Verdana",
        "Times New Roman", "Georgia", "Cambria",
        "Consolas", "Courier New", "Fira Code",
    };

    private static readonly (string Hex, global::Avalonia.Media.Color Color)[] ColorSwatches =
    {
        ("#FFFFFF", Colors.White),
        ("#FFFF00", Colors.Yellow),
        ("#00FFFF", Colors.Cyan),
        ("#00FF00", Colors.Lime),
        ("#FFA500", Colors.Orange),
        ("#FFC0CB", Colors.Pink),
        ("#E0E0E0", global::Avalonia.Media.Color.FromRgb(224, 224, 224)),
        ("#A0A0A0", global::Avalonia.Media.Color.FromRgb(160, 160, 160)),
    };

    public SubtitleSettingsDialog(ISubtitleManager mgr)
    {
        _mgr = mgr ?? throw new ArgumentNullException(nameof(mgr));
        InitializeComponent();
        BuildControls();
    }

    // ═══════════════════════════════════════════════
    //  Build all appearance controls (compact)
    // ═══════════════════════════════════════════════

    private void BuildControls()
    {
        BodyPanel.Children.Add(BuildSlider("Size", () => $"{_mgr.SubtitleFontScale:F1}\u00d7",
            _mgr.SubtitleFontScale, 0.5, 3.0, 0.1, v => _mgr.SubtitleFontScale = Math.Round(v, 1)));
        BodyPanel.Children.Add(BuildSlider("Position", () => $"{_mgr.SubtitlePosition}%",
            _mgr.SubtitlePosition, 0, 200, 1, v => _mgr.SubtitlePosition = (int)Math.Round(v)));
        BodyPanel.Children.Add(BuildSlider("Border", () => $"{_mgr.SubtitleBorderSize:F1}",
            _mgr.SubtitleBorderSize, 0, 5, 0.5, v => _mgr.SubtitleBorderSize = Math.Round(v, 1)));
        BodyPanel.Children.Add(BuildSlider("Shadow", () => $"{_mgr.SubtitleShadowOffset:F1}",
            _mgr.SubtitleShadowOffset, 0, 5, 0.5, v => _mgr.SubtitleShadowOffset = Math.Round(v, 1)));
        BodyPanel.Children.Add(BuildSlider("Opacity", () => $"{(int)(_mgr.SubtitleOpacity * 100)}%",
            _mgr.SubtitleOpacity, 0.0, 1.0, 0.1, v => _mgr.SubtitleOpacity = Math.Round(v, 1)));

        // ── Bold ──
        var boldCheck = new global::Avalonia.Controls.CheckBox
        {
            Content = "Bold",
            FontSize = Token.Size("font-size-caption"),
            IsChecked = _mgr.SubtitleBold,
            Margin = new Thickness(8, 0, 8, 0),
        };
        boldCheck.IsCheckedChanged += (_, _) => _mgr.SubtitleBold = boldCheck.IsChecked ?? false;
        BodyPanel.Children.Add(boldCheck);

        // ── Font + Color compact row ──
        var fontCombo = new global::Avalonia.Controls.ComboBox
        {
            ItemsSource = CommonFonts,
            SelectedItem = CommonFonts.Contains(_mgr.SubtitleFont) ? _mgr.SubtitleFont : CommonFonts[0],
            FontSize = Token.Size("font-size-caption"),
            MinHeight = 24,
            Background = GetThemeBrush("OverlayChrome"),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(8, 4, 8, 4),
        };
        fontCombo.SelectionChanged += (_, _) =>
        { if (fontCombo.SelectedItem is string f) _mgr.SubtitleFont = f; };
        BodyPanel.Children.Add(fontCombo);

        BodyPanel.Children.Add(BuildColorPicker());
    }

    // ═══════════════════════════════════════════════
    //  Slider row: "Label  value" + [−] [===] [+]   (compact)
    // ═══════════════════════════════════════════════

    private static StackPanel BuildSlider(
        string label, Func<string> formatValue,
        double initial, double min, double max, double tick,
        Action<double> onChanged)
    {
        var valueText = new TextBlock
        {
            Text = formatValue(),
            FontSize = Token.Size("font-size-body2"),
            Foreground = AppColors.TextPrimary,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
        };

        var header = new Grid
        {
            ColumnDefinitions = new("Auto,*,Auto"),
            Margin = new Thickness(8, 0, 6, 0)
        };
        var labelText = new TextBlock
        {
            Text = label,
            FontSize = Token.Size("font-size-caption"),
            Foreground = GetThemeBrush("OsdForeground"),
            Opacity = 0.5,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
        };
        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(valueText, 2);
        header.Children.Add(labelText);
        header.Children.Add(valueText);

        var slider = new global::Avalonia.Controls.Slider
        {
            Minimum = min, Maximum = max, Value = initial,
            TickFrequency = tick, IsSnapToTickEnabled = true,
            Height = 20,
            Margin = new Thickness(4, 0, 4, 0)
        };
        slider.Classes.Add("compact");
        slider.ValueChanged += (_, e) => { onChanged(e.NewValue); valueText.Text = formatValue(); };

        Button MakeBtn(string text) => new()
        {
            Content = new TextBlock { Text = text, FontSize = Token.Size("font-size-body2"), FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 18, Height = 18, CornerRadius = new(8),
            Background = AppColors.Transparent, BorderThickness = new(0), Padding = new(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };

        var btnMinus = MakeBtn("\u2212");
        var btnPlus = MakeBtn("+");
        btnMinus.Click += (_, _) => slider.Value = Math.Max(min, slider.Value - tick);
        btnPlus.Click += (_, _) => slider.Value = Math.Min(max, slider.Value + tick);
        btnMinus.PointerEntered += (_, _) => btnMinus.Background = AppColors.HoverSubtle;
        btnMinus.PointerExited += (_, _) => btnMinus.Background = AppColors.Transparent;
        btnPlus.PointerEntered += (_, _) => btnPlus.Background = AppColors.HoverSubtle;
        btnPlus.PointerExited += (_, _) => btnPlus.Background = AppColors.Transparent;

        var sliderRow = new Grid
        {
            ColumnDefinitions = new("Auto,*,Auto"),
            Margin = new Thickness(4, 0)
        };
        Grid.SetColumn(btnMinus, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(btnPlus, 2);
        sliderRow.Children.Add(btnMinus);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(btnPlus);

        return new StackPanel { Spacing = 0, Margin = new Thickness(0, 0), Children = { header, sliderRow } };
    }

    // ═══════════════════════════════════════════════
    //  Color picker (compact)
    // ═══════════════════════════════════════════════

    private StackPanel BuildColorPicker()
    {
        var currentHex = _mgr.SubtitleColor;
        if (!global::Avalonia.Media.Color.TryParse(currentHex, out var currentColor))
            currentColor = Colors.White;

        var colorPreview = new Border
        {
            Width = 16, Height = 16,
            CornerRadius = new(2),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new(1),
            Background = new SolidColorBrush(currentColor)
        };

        var colorInput = new global::Avalonia.Controls.TextBox
        {
            Text = _mgr.SubtitleColor,
            FontSize = Token.Size("font-size-body2"),
            MinWidth = 58, MaxWidth = 72,
            MinHeight = 20,
            Background = GetThemeBrush("OverlayChrome"),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new(1),
            CornerRadius = new(4),
            Foreground = GetThemeBrush("OsdForeground"),
            Padding = new(4, 0),
        };

        void ApplyColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            if (global::Avalonia.Media.Color.TryParse(hex, out var c))
            { _mgr.SubtitleColor = hex; colorPreview.Background = new SolidColorBrush(c); }
        }

        colorInput.TextChanged += (_, _) =>
        {
            var t = colorInput.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(t) && t.Length >= 4 && t.StartsWith("#"))
                ApplyColor(t);
        };

        // Swatch row
        var swatchPanel = new StackPanel { Orientation = AvaloniaLayout.Orientation.Horizontal, Margin = new(8, 3, 8, 3) };
        foreach (var (hex, color) in ColorSwatches)
        {
            var isSel = hex == _mgr.SubtitleColor;
            var swatch = new Border
            {
                Width = 16, Height = 16, CornerRadius = new(2), Margin = new(1.5, 0),
                BorderBrush = isSel ? GetThemeBrush("AccentColor") : GetThemeBrush("PopoverBorder"),
                BorderThickness = new(isSel ? 2 : 1),
                Background = new SolidColorBrush(color),
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            swatch.PointerPressed += (_, _) => { ApplyColor(hex); colorInput.Text = hex; };
            swatchPanel.Children.Add(swatch);
        }

        // Input row
        var inputRow = new Grid
        {
            ColumnDefinitions = new("*,Auto"),
            Margin = new(8, 0, 8, 0)
        };
        var lbl = new TextBlock
        {
            Text = "Color", FontSize = Token.Size("font-size-caption"), Foreground = GetThemeBrush("OsdForeground"),
            Opacity = 0.5, VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center,
        };
        var ctrls = new StackPanel { Orientation = AvaloniaLayout.Orientation.Horizontal, Spacing = 4 };
        ctrls.Children.Add(colorPreview);
        ctrls.Children.Add(colorInput);
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(ctrls, 1);
        inputRow.Children.Add(lbl);
        inputRow.Children.Add(ctrls);

        return new StackPanel { Margin = new(0, 1), Children = { swatchPanel, inputRow } };
    }

    // ═══════════════════════════════════════════════
    //  Button handlers
    // ═══════════════════════════════════════════════

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        _mgr.ResetAllSubtitles();
        BodyPanel.Children.Clear();
        BuildControls();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
