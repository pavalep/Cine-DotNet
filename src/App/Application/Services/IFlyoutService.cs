using System;
using Avalonia.Controls;
using Control = Avalonia.Controls.Control;

namespace Cine.Avalonia.Services;

/// <summary>
/// Abstraction over flyout lifecycle management — mutual exclusion,
/// registration, hide/show, and reopen support.
/// Implementations guarantee only one flyout is open at a time.
/// </summary>
public interface IFlyoutService
{
    /// <summary>Whether any flyout is currently open.</summary>
    bool HasActiveFlyouts { get; }

    /// <summary>The key of the currently-open flyout, or null.</summary>
    string? CurrentOpenKey { get; }

    /// <summary>
    /// Show a flyout: dismiss others, then invoke the show callback.
    /// </summary>
    void ShowFlyout(string key, Control anchor, Control content, bool placeAbove,
        Action<Control, Control, bool> showContent);

    /// <summary>Hide a flyout: mark closed, then invoke the hide callback.</summary>
    void HideFlyout(string key, Action? hideContent);

    /// <summary>Register a flyout source with an optional close action.</summary>
    void Register(string key, Action? closeAction = null);

    /// <summary>
    /// Close any other open flyout, then mark this key as open.
    /// Call BEFORE showing a flyout to ensure only one is visible.
    /// </summary>
    void DismissOthers(string key);

    /// <summary>Mark a flyout as closed (call from closed event or dismiss handler).</summary>
    void MarkClosed(string key);

    /// <summary>
    /// Close all open flyouts. Returns null if nothing was open,
    /// otherwise returns a reopen action to be called after the
    /// operation (file dialog, etc.) completes.
    /// </summary>
    Action? CloseAll();

    /// <summary>Set a reopen action for a registered flyout (for "close → dialog → reopen" cycle).</summary>
    void SetReopen(string key, Action reopen);
}
