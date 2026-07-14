namespace Simba.Avalonia.Core;

/// <summary>Published when a track is switched (audio or subtitle).</summary>
public sealed record TrackChangedEvent(string TrackType, string DisplayName);

/// <summary>Published when an external subtitle or audio track file is dropped into the player.</summary>
public sealed record ExternalTrackLoadedEvent(string FilePath, string TrackType);

/// <summary>Published when a player error occurs.</summary>
public sealed record PlayerErrorEvent(string ErrorMessage);

/// <summary>Published when the replay overlay is clicked and replay is requested.</summary>
public sealed record ReplayRequestedEvent;

/// <summary>Published when picture-in-picture mode is toggled.</summary>
public sealed record PipToggleEvent;

/// <summary>Published when the OSD notification is clicked.</summary>
public sealed record OsdClickedEvent(string Category);
