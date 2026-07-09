using System;
using System.Collections.Generic;

namespace Cine.Avalonia.State;

/// <summary>Persisted playlist data.</summary>
internal sealed record PlaylistData(
    int Version,
    List<string> Items,
    int CurrentPosition,
    DateTime? LastPlayed
);
