# Phase 10.10 — Drag-and-Drop Robustness

> **Goal**: Fix empty catch blocks, add magic-byte file validation, and ensure consistent visual feedback across all drag-drop targets. **Estimated effort: 1-2 hours**.

---

## Current State

| Entry Point | File | Status |
|-------------|------|--------|
| Window-level drag-drop | [`MainWindow.Core.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Shell/MainWindow.Core.cs) | ✅ Implemented |
| Drag-drop overlay | [`DragDropOverlayControl.axaml.cs`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/DragDropOverlayControl.axaml.cs) | ✅ Implemented |
| Playlist dialog drag-drop | [`PlaylistDialog.axaml.cs:233`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Dialogs/PlaylistDialog.axaml.cs#L233) | ❌ Empty catch blocks |
| Start page drag-drop | [`StartPage.axaml`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Screens/Start/StartPage.axaml) | ❌ Not implemented |
| File type validation | All entry points | ⚠️ Extension-only, no magic-byte check |

---

## Implementation Steps

### Step 1: Fix Empty Catch Blocks in PlaylistDialog

```csharp
// ❌ Before — PlaylistDialog.axaml.cs:233
catch { /* silently fail */ }

// ✅ After
catch (Exception ex)
{
    Log.ForContext<PlaylistDialog>()
        .Warning(ex, "Drag-drop operation failed in playlist");
    // Optionally show toast notification to user
}
```

### Step 2: Add Magic-Byte Validation

Extension-only filtering can be bypassed by renamed files. Add magic-byte (file signature) validation:

```csharp
public static class FileValidation
{
    private static readonly Dictionary<string, byte[]> MagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"]  = [0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], // ftyp
        [".mkv"]  = [0x1A, 0x45, 0xDF, 0xA3], // Matroska header
        [".avi"]  = [0x52, 0x49, 0x46, 0x46], // RIFF
        [".webm"] = [0x1A, 0x45, 0xDF, 0xA3], // Matroska (same as mkv)
        [".mov"]  = [0x00, 0x00, 0x00, 0x14, 0x66, 0x74, 0x79, 0x70], // ftyp
        [".wmv"]  = [0x30, 0x26, 0xB2, 0x75], // ASF header
    };

    public static bool IsValidMediaFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (!MagicBytes.ContainsKey(ext))
            return false; // Unknown extension

        try
        {
            using var fs = File.OpenRead(path);
            var header = new byte[8];
            var read = fs.Read(header, 0, header.Length);
            if (read < MagicBytes[ext].Length)
                return false;

            return header.Take(MagicBytes[ext].Length).SequenceEqual(MagicBytes[ext]);
        }
        catch
        {
            return false; // Can't read file — reject
        }
    }
}
```

### Step 3: Integrate Validation into Drag-Drop Pipeline

```csharp
// In MainWindow drag-drop handler
private void OnDrop(object? sender, DragEventArgs e)
{
    var files = e.Data.GetFiles()?
        .Select(f => f.Path.LocalPath)
        .Where(path => FileValidation.IsValidMediaFile(path))
        .ToArray();

    if (files == null || files.Length == 0)
    {
        // Show "invalid file" visual feedback
        return;
    }

    _viewModel?.OpenFiles(files);
}
```

### Step 4: Implement StartPage Drag-Drop (Optional)

If the StartPage should accept drag-drop:

```xml
<!-- StartPage.axaml -->
<Grid AllowDrop="True"
      DragEnter="OnDragEnter"
      DragLeave="OnDragLeave"
      Drop="OnDrop">
```

```csharp
private void OnDragEnter(object? sender, DragEventArgs e)
{
    if (e.Data.Contains(DataFormats.Files))
        e.DragEffects = DragDropEffects.Copy;
    else
        e.DragEffects = DragDropEffects.None;
}
```

### Step 5: Consistent Visual Feedback

Ensure all drag-drop targets show the same overlay/feedback:

| State | Visual Feedback |
|-------|----------------|
| Drag enters valid area | Semi-transparent overlay + border highlight |
| Invalid file type | Red border + "X" cursor indication |
| Drop accepted | Brief flash animation, then normal state |
| Drop rejected | Shake animation or red flash |

The existing [`DragDropOverlayControl`](file:///x:/Development/Cine_CSharp_DotNet/src/App/UI/Controls/Indicators/DragDropOverlayControl.axaml) already provides this — ensure it's used consistently across all targets.

---

## Success Criteria

- [ ] Empty catch blocks in `PlaylistDialog.axaml.cs` fixed with proper logging
- [ ] Magic-byte validation for all supported media formats
- [ ] Drag-drop pipeline validates files before accepting
- [ ] Consistent visual feedback across all drag-drop targets
- [ ] Invalid files are rejected with clear user feedback
