using System.IO;

namespace Cine.Avalonia.Models;

/// <summary>
/// Lightweight display model for a recent file shown in the StartPage card list.
/// Properties are pre-computed once at construction so the DataTemplate needs no converters.
/// </summary>
public sealed class RecentFileItem
{
    public string FilePath  { get; }
    public string Title     { get; }
    public string Extension { get; }
    public bool   IsVideo   { get; }

    public RecentFileItem(string filePath, bool isVideo)
    {
        FilePath  = filePath;
        Title     = Path.GetFileNameWithoutExtension(filePath);
        Extension = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
        IsVideo   = isVideo;
    }
}
