using Cine.Core.Interfaces;
using Cine.Media.Interfaces;

namespace Cine.WinUI.Services;
public static class ServiceLocator
{
    public static IConfigService? ConfigService { get; private set; }
    public static IMediaPlayer? MediaPlayer { get; private set; }

    public static void Initialize(IConfigService config, IMediaPlayer media)
    {
        ConfigService = config;
        MediaPlayer = media;
    }
}