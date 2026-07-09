namespace Cine.Avalonia.Serialization;

/// <summary>Persisted PiP window position and state.</summary>
internal sealed record PipState(int X, int Y, int W, int H, bool Pinned);
