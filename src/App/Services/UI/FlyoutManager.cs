using System;
using System.Collections.Generic;

namespace Cine.Avalonia.Services;

/// <summary>
/// Ensures only ONE flyout is open at a time across the entire application.
///
/// Usage: Each flyout source registers itself with a unique key and a close action.
/// Before showing a flyout, the click handler calls DismissOthers(key) —
/// this auto-closes any other currently-open flyout, creating the
/// professional "one surface at a time" UX contract.
///
/// For the file-dialog deadlock workaround (Avalonia #18969), call
/// CloseAll() before opening a native dialog. Returns a reopen action
/// if any flyout was active.
/// </summary>
public class FlyoutManager : IFlyoutService
{
    private readonly Dictionary<string, FlyoutEntry> _entries = new();
    private readonly object _lock = new();
    private string? _openKey;

    /// <summary>Whether any flyout is currently open (single source of truth).</summary>
    public bool HasActiveFlyouts
    {
        get { lock (_lock) { return _openKey != null; } }
    }

    /// <summary>The key of the currently-open flyout, or null.</summary>
    public string? CurrentOpenKey
    {
        get { lock (_lock) { return _openKey; } }
    }

    /// <summary>Show a flyout: dismiss others, register, then invoke the show callback.</summary>
    public void ShowFlyout(string key,
        global::Avalonia.Controls.Control anchor,
        global::Avalonia.Controls.Control content,
        bool placeAbove,
        Action<global::Avalonia.Controls.Control, global::Avalonia.Controls.Control, bool> showContent)
    {
        DismissOthers(key);
        showContent(anchor, content, placeAbove);
    }

    /// <summary>Hide a flyout: mark closed, then invoke the hide callback.</summary>
    public void HideFlyout(string key, Action? hideContent)
    {
        MarkClosed(key);
        hideContent?.Invoke();
    }

    /// <summary>
    /// Register a flyout source with a close action.
    /// </summary>
    public void Register(string key, Action? closeAction = null)
    {
        lock (_lock)
            _entries[key] = new FlyoutEntry(key, closeAction);
    }

    /// <summary>
    /// Close any other open flyout, then mark this key as open.
    /// Call BEFORE ShowAt() to ensure only this flyout is visible.
    /// </summary>
    public void DismissOthers(string key)
    {
        Action? closeAction;
        lock (_lock)
        {
            closeAction = null;
            if (_openKey != null && _openKey != key && _entries.TryGetValue(_openKey, out var entry))
            {
                closeAction = entry.TryClose;
                entry.IsOpen = false;
            }
            if (_entries.TryGetValue(key, out var thisEntry))
                thisEntry.IsOpen = true;
            _openKey = key;
        }

        // Invoke close action outside lock to prevent deadlock
        closeAction?.Invoke();
    }

    /// <summary>
    /// Mark a flyout as closed (call from Flyout.Closed event).
    /// </summary>
    public void MarkClosed(string key)
    {
        lock (_lock)
        {
            if (_openKey == key) _openKey = null;
            if (_entries.TryGetValue(key, out var entry)) entry.IsOpen = false;
        }
    }

    /// <summary>
    /// Close all open flyouts. Returns null if nothing was open,
    /// otherwise returns a reopen action to be called after the
    /// operation (file dialog, etc.) completes.
    /// </summary>
    public Action? CloseAll()
    {
        string? toReopen;
        Action? closeAction;
        lock (_lock)
        {
            toReopen = _openKey;
            closeAction = null;
            if (toReopen != null && _entries.TryGetValue(toReopen, out var entry))
            {
                closeAction = entry.TryClose;
                entry.IsOpen = false;
            }
            _openKey = null;
        }

        // Invoke close action outside lock to prevent deadlock
        closeAction?.Invoke();

        if (toReopen == null) return null;

        var keyToReopen = toReopen;
        return () =>
        {
            lock (_lock)
            {
                if (_entries.TryGetValue(keyToReopen, out var entry) && entry.ReopenAction != null)
                {
                    entry.ReopenAction();
                    entry.IsOpen = true;
                    _openKey = keyToReopen;
                }
            }
        };
    }

    /// <summary>
    /// Set a reopen action for a registered flyout (for "close → dialog → reopen" cycle).
    /// </summary>
    public void SetReopen(string key, Action reopen)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var entry))
                entry.ReopenAction = reopen;
        }
    }

    private class FlyoutEntry
    {
        public string Key { get; }
        public bool IsOpen;
        public Action? ReopenAction;
        private readonly Action? _closeAction;

        public FlyoutEntry(string key, Action? closeAction)
        {
            Key = key;
            _closeAction = closeAction;
        }

        public void TryClose() => _closeAction?.Invoke();
    }
}
