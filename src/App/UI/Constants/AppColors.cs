using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using Avalonia.Media;
using Cine.Avalonia.Serialization;

namespace Cine.Avalonia;

public static class AppColors
{
    private static readonly FrozenDictionary<string, SolidColorBrush> _colors;

    static AppColors()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName().Name + ".UI.Constants.Colors.json";
        using var stream = asm.GetManifestResourceStream(name)!;
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;

        _colors = dict.ToFrozenDictionary(
            kv => kv.Key,
            kv => new SolidColorBrush(global::Avalonia.Media.Color.Parse(kv.Value)),
            StringComparer.OrdinalIgnoreCase);

        // System brushes
        White = _colors.GetValueOrDefault("White") ?? new SolidColorBrush(Colors.White);
        Black = _colors.GetValueOrDefault("Black") ?? new SolidColorBrush(Colors.Black);
        Transparent = _colors.GetValueOrDefault("Transparent") ?? new SolidColorBrush(Colors.Transparent);
    }

    // ── Auto-generated from JSON ──
    public static SolidColorBrush Background        => _colors["Background"];
    public static SolidColorBrush Surface           => _colors["Surface"];
    public static SolidColorBrush SurfaceLight      => _colors["SurfaceLight"];
    public static SolidColorBrush DialogSurface     => _colors["DialogSurface"];

    public static SolidColorBrush Accent            => _colors["Accent"];
    public static SolidColorBrush AccentDim         => _colors["AccentDim"];
    public static SolidColorBrush AccentLight       => _colors["AccentLight"];
    public static SolidColorBrush DragAccent        => _colors["DragAccent"];
    public static SolidColorBrush DragAccentDim     => _colors["DragAccentDim"];

    public static SolidColorBrush TextPrimary       => _colors["TextPrimary"];
    public static SolidColorBrush TextSecondary     => _colors["TextSecondary"];
    public static SolidColorBrush TextTertiary      => _colors["TextTertiary"];
    public static SolidColorBrush TextOnDarkPrimary => _colors["TextOnDarkPrimary"];
    public static SolidColorBrush TextOnDarkSecondary => _colors["TextOnDarkSecondary"];
    public static SolidColorBrush TextOnDarkHint    => _colors["TextOnDarkHint"];
    public static SolidColorBrush TextOnDarkDisabled => _colors["TextOnDarkDisabled"];
    public static SolidColorBrush TextOnDarkTertiary => _colors["TextOnDarkTertiary"];
    public static SolidColorBrush TextOnDarkSubtle  => _colors["TextOnDarkSubtle"];
    public static SolidColorBrush TextMuted         => _colors["TextMuted"];
    public static SolidColorBrush TextOnAccent      => _colors["TextOnAccent"];

    public static SolidColorBrush Overlay           => _colors["Overlay"];
    public static SolidColorBrush OverlayLight      => _colors["OverlayLight"];
    public static SolidColorBrush OverlayDark       => _colors["OverlayDark"];
    public static SolidColorBrush OverlayOpaque     => _colors["OverlayOpaque"];
    public static SolidColorBrush OverlayChrome     => _colors["OverlayChrome"];

    public static SolidColorBrush Divider           => _colors["Divider"];
    public static SolidColorBrush DividerStrong     => _colors["DividerStrong"];
    public static SolidColorBrush BorderLight       => _colors["BorderLight"];
    public static SolidColorBrush BorderDim         => _colors["BorderDim"];

    public static SolidColorBrush HoverSubtle       => _colors["HoverSubtle"];
    public static SolidColorBrush Hover             => _colors["Hover"];
    public static SolidColorBrush HoverStrong       => _colors["HoverStrong"];
    public static SolidColorBrush Pressed           => _colors["Pressed"];

    public static SolidColorBrush IconLight         => _colors["IconLight"];
    public static SolidColorBrush IconDim           => _colors["IconDim"];

    // ── System ──
    public static SolidColorBrush White { get; }
    public static SolidColorBrush Black { get; }
    public static SolidColorBrush Transparent { get; }

    // ── Utility ──
    public static SolidColorBrush Parse(string hex) => new(global::Avalonia.Media.Color.Parse(hex));
    public static global::Avalonia.Media.Color ToColor(SolidColorBrush brush) => brush.Color;
}
