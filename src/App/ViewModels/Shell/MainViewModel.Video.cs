using System;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.ViewModels;

/// <summary>
/// Video adjustment properties and commands: contrast, brightness, gamma,
/// saturation, hue, zoom, aspect ratio, crop, rotation, flip.
/// Bound to the video adjustment panel and keyboard shortcuts.
/// </summary>
public partial class MainViewModel
{
    public double ContrastValue
    {
        get => _player.Contrast;
        set { _player.Contrast = value; OnPropertyChanged(); }
    }

    public double BrightnessValue
    {
        get => _player.Brightness;
        set { _player.Brightness = value; OnPropertyChanged(); }
    }

    public double GammaValue
    {
        get => _player.Gamma;
        set { _player.Gamma = value; OnPropertyChanged(); }
    }

    public double SaturationValue
    {
        get => _player.Saturation;
        set { _player.Saturation = value; OnPropertyChanged(); }
    }

    public double HueValue
    {
        get => _player.Hue;
        set { _player.Hue = value; OnPropertyChanged(); }
    }

    public double ZoomValue
    {
        get => _player.Zoom;
        set { _player.Zoom = value; OnPropertyChanged(); }
    }

    public double AspectRatioValue
    {
        get => _player.AspectRatio;
        set
        {
            _player.AspectRatio = value;
            OnPropertyChanged();
            UpdateCropFilter();
        }
    }

    // --- Rotation & Flip ---
    public void ResetAspectRatio() => AspectRatioValue = -1;
    public void SetAspectRatio(double ratio) => AspectRatioValue = ratio;

    // ── Crop (removes black bars, VLC-style) ──
    private const string CropFilterLabel = "@crop";
    private double _cropValue = -1;

    public double CropValue
    {
        get => _cropValue;
        set { _cropValue = value; OnPropertyChanged(); }
    }

    public void SetCrop(double aspectRatio)
    {
        CropValue = aspectRatio;
        UpdateCropFilter();
    }

    public void ResetCrop()
    {
        CropValue = -1;
        UpdateCropFilter();
    }

    public void UpdateCropFilter()
    {
        if (_cropValue <= 0)
        {
            _player.Command("vf", "remove", CropFilterLabel);
        }
        else
        {
            // First remove to prevent duplicates/errors
            _player.Command("vf", "remove", CropFilterLabel);

            double R = _cropValue;
            double A = AspectRatioValue; // -1 or positive override

            if (A > 0)
            {
                // Aspect ratio is overridden
                string filter;
                if (A > R)
                {
                    double ratio = R / A;
                    string ratioStr = ratio.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    filter = $"{CropFilterLabel}:crop=w=iw*{ratioStr}:h=ih";
                }
                else
                {
                    double ratio = A / R;
                    string ratioStr = ratio.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                    filter = $"{CropFilterLabel}:crop=w=iw:h=ih*{ratioStr}";
                }
                _player.Command("vf", "add", filter);
            }
            else
            {
                // Aspect ratio is original
                string rStr = R.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);
                string filter = $"{CropFilterLabel}:crop=w=if(gt(iw/ih\\,{rStr})\\,ih*{rStr}\\,iw):h=if(gt(iw/ih\\,{rStr})\\,ih\\,iw/{rStr})";
                _player.Command("vf", "add", filter);
            }
        }
    }

    public void RotateLeft() => _player.Command("set", "video-rotate", "90");
    public void RotateRight() => _player.Command("set", "video-rotate", "270");
    public void ResetRotation() => _player.Command("set", "video-rotate", "0");
    public void FlipHorizontal() => _player.Command("vf", "toggle", "hflip");
    public void FlipVertical() => _player.Command("vf", "toggle", "vflip");
    public void ResetFlip() => _player.Command("vf", "del", "@hflip", "@vflip");
    public void ResetZoom() => ZoomValue = 0;

    // --- Reset Commands (video-only) ---
    public void ResetContrast() => ContrastValue = 0;
    public void ResetBrightness() => BrightnessValue = 0;
    public void ResetGamma() => GammaValue = 1;
    public void ResetSaturation() => SaturationValue = 1;
    public void ResetHue() => HueValue = 0;
}
