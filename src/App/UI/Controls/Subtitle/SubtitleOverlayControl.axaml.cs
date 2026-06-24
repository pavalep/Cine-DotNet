using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Cine.Avalonia.Builders;
using Cine.Avalonia.Services;
using Cine.Avalonia.ViewModels;
using Cine.Core.Services;
using DragEventArgs = Avalonia.Input.DragEventArgs;
using DragDropEffects = Avalonia.Input.DragDropEffects;
using AvaloniaLayout = Avalonia.Layout;
using Button = global::Avalonia.Controls.Button;
using Cursor = Avalonia.Input.Cursor;

namespace Cine.Avalonia.Controls;

/// <summary>
/// Standalone subtitle overlay layer with its own button + flyout containing
/// subtitle track selection, delay controls, and an Appearance submenu for
/// styling (font size, position, font, border, shadow, color).
/// Supports drag-drop of external subtitle files (.srt, .ass, .vtt, .sub, .idx).
/// </summary>
public partial class SubtitleOverlayControl : AvaloniaUserControl
{
    private readonly ILogger _log;
    private MainViewModel? _viewModel;
    private Flyout? _currentFlyout;

    /// <summary>
    /// Safely resolves a theme resource brush. Returns null (instead of
    /// throwing InvalidCastException) when the resource key is missing or
    /// its value is not an IBrush.
    /// </summary>
    private static IBrush? GetThemeBrush(string key)
    {
        var result = global::Avalonia.Application.Current?.FindResource(key);
        return result as IBrush;
    }

    private static readonly string[] SubtitleExtensions = { ".srt", ".ass", ".ssa", ".vtt", ".sub", ".idx" };

    /// <summary>Comprehensive set of common font families for subtitle display.</summary>
    private static readonly string[] CommonFonts =
    {
        // Sans-serif
        "Arial", "Arial Black", "Calibri", "Candara", "Century Gothic",
        "Franklin Gothic", "Futura", "Geneva", "Helvetica", "Helvetica Neue",
        "Impact", "Lucida Grande", "Segoe UI", "Tahoma", "Trebuchet MS",
        "Verdana",
        // Serif
        "Cambria", "Didot", "Garamond", "Georgia", "Palatino",
        "Palatino Linotype", "Times New Roman",
        // Monospace
        "Consolas", "Courier New", "DejaVu Sans Mono", "Fira Code",
        "JetBrains Mono", "Lucida Console", "Monaco", "Source Code Pro",
        // Display / decorative
        "Comic Sans MS",
    };

    private static readonly (string Hex, global::Avalonia.Media.Color Color)[] ColorSwatches =
    {
        ("#FFFFFF", global::Avalonia.Media.Colors.White),
        ("#FFFF00", global::Avalonia.Media.Colors.Yellow),
        ("#00FFFF", global::Avalonia.Media.Colors.Cyan),
        ("#00FF00", global::Avalonia.Media.Colors.Lime),
        ("#FFA500", global::Avalonia.Media.Colors.Orange),
        ("#FFC0CB", global::Avalonia.Media.Colors.Pink),
        ("#E0E0E0", global::Avalonia.Media.Color.FromRgb(224, 224, 224)),
        ("#C0C0C0", global::Avalonia.Media.Color.FromRgb(192, 192, 192)),
        ("#A0A0A0", global::Avalonia.Media.Color.FromRgb(160, 160, 160)),
    };

    /// <summary>
    /// Fired when an external subtitle file is dropped onto the button.
    /// MainWindow subscribes to this to show OSD notifications.
    /// </summary>
    public event EventHandler<string>? ExternalFileDropped;

    public SubtitleOverlayControl()
    {
        _log = global::Cine.Core.Log.ForContext<SubtitleOverlayControl>();
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // Wire drag-drop on the button
        DragDrop.SetAllowDrop(BtnSubtitles, true);
        BtnSubtitles.AddHandler(DragDrop.DragOverEvent, OnBtnDragOver);
        BtnSubtitles.AddHandler(DragDrop.DropEvent, OnBtnDrop);
        _log.Debug("Constructor: initialized with drag-drop support");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _viewModel = DataContext as MainViewModel;
        _log.Trace("OnDataContextChanged: vm={Vm}", _viewModel != null ? "set" : "null");
    }

    /// <summary>Refreshes the subtitle icon to reflect enabled/disabled state.</summary>
    public void RefreshIcon()
    {
        if (_viewModel == null) return;
        if (SubtitleIcon == null) return;
        SubtitleIcon.Kind = _viewModel.IsSubtitleEnabled
            ? Material.Icons.MaterialIconKind.Subtitles
            : Material.Icons.MaterialIconKind.ClosedCaptionOutline;
    }

    public void SetVisibility(bool visible) => IsVisible = visible;

    /// <summary>Closes the flyout if it is currently open.</summary>
    public void HideFlyout()
    {
        if (_currentFlyout?.IsOpen == true)
        {
            _log.Trace("HideFlyout: hiding subtitle flyout");
            _currentFlyout.Hide();
        }
    }

    private void OnSubtitlesClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.Subtitles == null)
            {
                _log.Warning("OnSubtitlesClick: _viewModel or Subtitles is null");
                return;
            }
            _log.Debug("OnSubtitlesClick: building and showing subtitle flyout");
            _currentFlyout = BuildSubtitleFlyout();
            _currentFlyout.ShowAt(BtnSubtitles);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "OnSubtitlesClick: exception building/showing flyout");
        }
    }

    // ═══════════════════════════════════════════════
    //  Flyout Construction
    // ═══════════════════════════════════════════════

    private Flyout BuildSubtitleFlyout()
    {
        var mgr = _viewModel!.Subtitles;
        return TrackFlyoutBuilder.Build(
            mgr.SubtitleTracks,
            "No subtitles available",
            "Subtitle Delay",
            () => mgr.SubtitleDelay,
            v => mgr.SubtitleDelay = (float)Math.Clamp(v, -10, 10),
            () => mgr.SubtitleDelay = 0,
            appendExtra: root => AppendAppearancePanel(root, mgr)
        );
    }

    /// <summary>
    /// Appends a separator + all appearance controls inline into the flyout root panel.
    /// No sub-flyout — avoids Avalonia's broken cascading flyout behavior.
    /// </summary>
    private static void AppendAppearancePanel(StackPanel root, ISubtitleManager mgr)
    {
        // Thin line separator (avoid templated Separator which can crash in some themes)
        root.Children.Add(new Border
        {
            Height = 1,
            Background = GetThemeBrush("PopoverBorder"),
            Margin = new Thickness(4, 2)
        });

        // "Appearance" header
        root.Children.Add(new TextBlock
        {
            Text = "Appearance",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetThemeBrush("OsdForeground"),
            Margin = new Thickness(8, 2, 8, 0),
            Opacity = 0.5,
        });

        // ── Font Size ──
        root.Children.Add(BuildSliderRow(
            "Font Size",
            () => $"{mgr.SubtitleFontScale:F1}×",
            mgr.SubtitleFontScale, 0.5, 3.0, 0.1,
            v => mgr.SubtitleFontScale = Math.Round(v, 1)));

        // ── Position ──
        root.Children.Add(BuildSliderRow(
            "Position",
            () => $"{mgr.SubtitlePosition}%",
            mgr.SubtitlePosition, 0, 200, 1,
            v => mgr.SubtitlePosition = (int)Math.Round(v)));

        // ── Border ──
        root.Children.Add(BuildSliderRow(
            "Border",
            () => $"{mgr.SubtitleBorderSize:F1}",
            mgr.SubtitleBorderSize, 0, 5, 0.5,
            v => mgr.SubtitleBorderSize = Math.Round(v, 1)));

        // ── Shadow ──
        root.Children.Add(BuildSliderRow(
            "Shadow",
            () => $"{mgr.SubtitleShadowOffset:F1}",
            mgr.SubtitleShadowOffset, 0, 5, 0.5,
            v => mgr.SubtitleShadowOffset = Math.Round(v, 1)));

        // ── Opacity ──
        root.Children.Add(BuildSliderRow(
            "Opacity",
            () => $"{mgr.SubtitleOpacity:P0}",
            mgr.SubtitleOpacity, 0.0, 1.0, 0.1,
            v => mgr.SubtitleOpacity = Math.Round(v, 1)));

        // ── Blur ──
        root.Children.Add(BuildSliderRow(
            "Blur",
            () => $"{mgr.SubtitleBlur:F1}",
            mgr.SubtitleBlur, 0.0, 20.0, 1.0,
            v => mgr.SubtitleBlur = Math.Round(v, 1)));

        // ── Bold ──
        var boldCheck = new global::Avalonia.Controls.CheckBox
        {
            Content = new TextBlock
            {
                Text = "Bold",
                FontSize = 11,
                Foreground = GetThemeBrush("OsdForeground"),
            },
            IsChecked = mgr.SubtitleBold,
            Margin = new Thickness(8, 2),
            Foreground = GetThemeBrush("OsdForeground"),
        };
        boldCheck.IsCheckedChanged += (_, _) =>
        {
            mgr.SubtitleBold = boldCheck.IsChecked ?? false;
            boldCheck.Content = new TextBlock
            {
                Text = boldCheck.IsChecked == true ? "Bold: On" : "Bold",
                FontSize = 11,
                Foreground = GetThemeBrush("OsdForeground"),
            };
        };
        mgr.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ISubtitleManager.SubtitleBold))
            {
                boldCheck.IsChecked = mgr.SubtitleBold;
                boldCheck.Content = new TextBlock
                {
                    Text = mgr.SubtitleBold ? "Bold: On" : "Bold",
                    FontSize = 11,
                    Foreground = GetThemeBrush("OsdForeground"),
                };
            }
        };
        root.Children.Add(boldCheck);

        // ── Font ──
        var systemFonts = CommonFonts;
        var fontCombo = new global::Avalonia.Controls.ComboBox
        {
            ItemsSource = systemFonts,
            SelectedItem = systemFonts.Contains(mgr.SubtitleFont)
                ? mgr.SubtitleFont
                : systemFonts.FirstOrDefault(),
            FontSize = 12,
            MinHeight = 28,
            Background = GetThemeBrush("OverlayChrome"),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(8, 0)
        };
        fontCombo.SelectionChanged += (_, _) =>
        {
            if (fontCombo.SelectedItem is string font)
                mgr.SubtitleFont = font;
        };

        var fontRow = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2) };
        fontRow.Children.Add(new TextBlock
        {
            Text = "Font",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetThemeBrush("OsdForeground"),
            Opacity = 0.5,
            Margin = new Thickness(8, 0)
        });
        fontRow.Children.Add(fontCombo);
        root.Children.Add(fontRow);

        // ── Color ──
        var colorPanel = BuildColorRow(mgr);
        root.Children.Add(colorPanel);

        // ── Reset ──
        root.Children.Add(new Border
        {
            Height = 1,
            Background = GetThemeBrush("PopoverBorder"),
            Margin = new Thickness(4, 4, 4, 2)
        });

        var resetBtn = new Button
        {
            Content = new TextBlock
            {
                Text = "Reset to Defaults",
                FontSize = 11,
                Foreground = AppColors.TextTertiary,
                HorizontalAlignment = AvaloniaLayout.HorizontalAlignment.Center
            },
            Background = AppColors.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow),
            Margin = new Thickness(8, 0, 8, 4)
        };
        resetBtn.PointerEntered += (_, _) => resetBtn.Background = AppColors.HoverSubtle;
        resetBtn.PointerExited += (_, _) => resetBtn.Background = AppColors.Transparent;
        resetBtn.Click += (_, _) => mgr.ResetAllSubtitles();
        root.Children.Add(resetBtn);
    }

    /// <summary>Builds a labeled slider row with +/- nudge buttons: "Label  value" + [−] [===] [+].</summary>
    private static StackPanel BuildSliderRow(
        string label, Func<string> formatValue,
        double initial, double min, double max, double tick,
        Action<double> onChanged)
    {
        var valueText = new TextBlock
        {
            Text = formatValue(),
            FontSize = 11,
            Foreground = GetThemeBrush("OsdForeground"),
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(8, 0)
        };
        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetThemeBrush("OsdForeground"),
            Opacity = 0.5
        };
        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(valueText, 1);
        headerGrid.Children.Add(labelText);
        headerGrid.Children.Add(valueText);

        var slider = new global::Avalonia.Controls.Slider
        {
            Minimum = min,
            Maximum = max,
            Value = initial,
            TickFrequency = tick,
            IsSnapToTickEnabled = true,
            Height = 28,
            Margin = new Thickness(4, 0)
        };
        slider.ValueChanged += (_, e) =>
        {
            onChanged(e.NewValue);
            valueText.Text = formatValue();
        };

        // ── Nudge buttons ──
        var btnMinus = new Button
        {
            Content = new TextBlock { Text = "\u2212", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };
        var btnPlus = new Button
        {
            Content = new TextBlock { Text = "+", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = AppColors.TextPrimary },
            Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
            Background = AppColors.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(0),
            HorizontalContentAlignment = AvaloniaLayout.HorizontalAlignment.Center,
            VerticalContentAlignment = AvaloniaLayout.VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Arrow)
        };

        btnMinus.Click += (_, _) =>
        {
            slider.Value = Math.Max(min, slider.Value - tick);
        };
        btnPlus.Click += (_, _) =>
        {
            slider.Value = Math.Min(max, slider.Value + tick);
        };
        btnMinus.PointerEntered += (_, _) => btnMinus.Background = AppColors.HoverSubtle;
        btnMinus.PointerExited += (_, _) => btnMinus.Background = AppColors.Transparent;
        btnPlus.PointerEntered += (_, _) => btnPlus.Background = AppColors.HoverSubtle;
        btnPlus.PointerExited += (_, _) => btnPlus.Background = AppColors.Transparent;

        var sliderRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(2, 0)
        };
        Grid.SetColumn(btnMinus, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(btnPlus, 2);
        sliderRow.Children.Add(btnMinus);
        sliderRow.Children.Add(slider);
        sliderRow.Children.Add(btnPlus);

        var panel = new StackPanel { Spacing = 0, Margin = new Thickness(0, 2) };
        panel.Children.Add(headerGrid);
        panel.Children.Add(sliderRow);
        return panel;
    }

    /// <summary>Builds a color picker row: label + preset swatches + hex text box.</summary>
    private static StackPanel BuildColorRow(ISubtitleManager mgr)
    {
        // Current color preview
        var currentHex = mgr.SubtitleColor;
        if (!global::Avalonia.Media.Color.TryParse(currentHex, out var currentColor))
            currentColor = global::Avalonia.Media.Colors.White;

        var colorPreview = new Border
        {
            Width = 18, Height = 18,
            CornerRadius = new CornerRadius(3),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(currentColor)
        };

        // Hex text input (kept for custom colors not in swatches)
        var colorInput = new global::Avalonia.Controls.TextBox
        {
            Text = mgr.SubtitleColor,
            FontSize = 11,
            MinWidth = 65,
            MaxWidth = 90,
            MinHeight = 22,
            Background = GetThemeBrush("OverlayChrome"),
            BorderBrush = GetThemeBrush("PopoverBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Foreground = GetThemeBrush("OsdForeground")
        };

        void ApplyColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return;
            if (global::Avalonia.Media.Color.TryParse(hex, out var color))
            {
                mgr.SubtitleColor = hex;
                colorPreview.Background = new SolidColorBrush(color);
            }
        }

        colorInput.TextChanged += (_, _) =>
        {
            var text = colorInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.StartsWith("#") && (text.Length == 4 || text.Length == 7 || text.Length == 9))
                ApplyColor(text);
        };

        // ── Swatch row ──
        var swatchPanel = new StackPanel
        {
            Orientation = AvaloniaLayout.Orientation.Horizontal,
            Margin = new Thickness(8, 2, 8, 0)
        };

        foreach (var (hex, color) in ColorSwatches)
        {
            var swatch = new Border
            {
                Width = 20, Height = 20,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2, 0),
                BorderBrush = hex == mgr.SubtitleColor
                    ? GetThemeBrush("AccentColor")
                    : GetThemeBrush("PopoverBorder"),
                BorderThickness = new Thickness(hex == mgr.SubtitleColor ? 2 : 1),
                Background = new SolidColorBrush(color),
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            swatch.PointerPressed += (_, _) =>
            {
                ApplyColor(hex);
                colorInput.Text = hex;
            };
            swatch.PointerEntered += (_, _) =>
            {
                swatch.BorderBrush = GetThemeBrush("AccentColor");
                swatch.BorderThickness = new Thickness(2);
            };
            swatch.PointerExited += (_, _) =>
            {
                swatch.BorderBrush = GetThemeBrush("PopoverBorder");
                swatch.BorderThickness = new Thickness(hex == mgr.SubtitleColor ? 2 : 1);
            };

            swatchPanel.Children.Add(swatch);
        }

        // ── Label + input row ──
        var inputRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Thickness(8, 2, 8, 2)
        };
        var colorLabel = new TextBlock
        {
            Text = "Color",
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = GetThemeBrush("OsdForeground"),
            Opacity = 0.5,
            VerticalAlignment = AvaloniaLayout.VerticalAlignment.Center
        };
        var inputControls = new StackPanel
        {
            Orientation = AvaloniaLayout.Orientation.Horizontal,
            Spacing = 4
        };
        inputControls.Children.Add(colorPreview);
        inputControls.Children.Add(colorInput);

        Grid.SetColumn(colorLabel, 0);
        Grid.SetColumn(inputControls, 1);
        inputRow.Children.Add(colorLabel);
        inputRow.Children.Add(inputControls);

        return new StackPanel { Children = { swatchPanel, inputRow } };
    }

    // ═══════════════════════════════════════════════
    //  Drag-drop handlers
    // ═══════════════════════════════════════════════

    private void OnBtnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        var hasValidFile = files.Any(f =>
        {
            var ext = Path.GetExtension(f.Path.LocalPath)?.ToLowerInvariant();
            return ext != null && SubtitleExtensions.Contains(ext);
        });

        e.DragEffects = hasValidFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        _log.Trace("OnBtnDragOver: validFile={ValidFile}", hasValidFile);
    }

    private async void OnBtnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer == null || !e.DataTransfer.Contains(DataFormat.File))
            return;

        var files = e.DataTransfer.TryGetFiles();
        if (files == null) return;

        foreach (var file in files)
        {
            var path = file.Path.LocalPath;
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != null && SubtitleExtensions.Contains(ext))
            {
                _log.Info("OnBtnDrop: dropping subtitle file {Path}", path);
                if (_viewModel?.Subtitles != null)
                    await _viewModel.Subtitles.LoadExternalSubtitleAsync(path);
                ExternalFileDropped?.Invoke(this, path);
                e.Handled = true;
            }
            else
            {
                _log.Trace("OnBtnDrop: ignored unsupported file {Path} (ext={Ext})", path, ext);
            }
        }
    }
}
