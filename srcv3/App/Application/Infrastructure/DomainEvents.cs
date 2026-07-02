namespace Cine.Avalonia.Infrastructure;

/// <summary>Published when a track is switched (audio or subtitle).</summary>
public sealed record TrackChangedEvent(string TrackType, string DisplayName);

/// <summary>Published before a file dialog opens — handler should dismiss any open flyouts.</summary>
public sealed record FlyoutDismissRequestEvent(string SourceKey);

/// <summary>Published when a file dialog result is needed for adding external files.</summary>
public sealed record FileDialogRequestEvent(string FileType);

/// <summary>Published when a media file is opened and the player is ready.</summary>
public sealed record MediaOpenedEvent(string FilePath);

/// <summary>Published when playback state changes.</summary>
public sealed record PlaybackStateChangedEvent(Media.Models.PlaybackState NewState);
