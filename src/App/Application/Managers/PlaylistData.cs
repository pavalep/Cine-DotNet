using System;
using System.Collections.Generic;

namespace Cine.Avalonia.Managers;

/// <summary>Persisted playlist data.</summary>
internal sealed record PlaylistData(
    int Version,
    List<string> Items,
    int CurrentPosition,
    DateTime? LastPlayed
);
