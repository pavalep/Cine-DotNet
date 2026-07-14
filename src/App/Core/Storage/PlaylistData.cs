using System;
using System.Collections.Generic;

namespace Simba.Avalonia.Storage;

/// <summary>Persisted playlist data.</summary>
internal sealed record PlaylistData(
    int Version,
    List<string> Items,
    int CurrentPosition,
    DateTime? LastPlayed
);
