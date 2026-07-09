using Avalonia.Input;
using KeyEventArgs = Avalonia.Input.KeyEventArgs;
using KeyModifiers = Avalonia.Input.KeyModifiers;

namespace Cine.Avalonia.Services;

/// <summary>
/// Application-wide keyboard shortcut router with scope support.
/// 
/// Shortcuts are registered once (typically in MainWindow.ctor) and routed
/// through <see cref="TryHandle"/>. Scopes allow blocking certain shortcuts
/// when dialogs are open, text controls are focused, or PIP is active.
///
/// Phase 3: Stack-based scope management. PushScope/PopScope replaces
/// manual scope detection in OnKeyDown. The scope stack is pushed when a
/// dialog opens and popped when it closes — ensuring nested dialogs work
/// correctly without scope confusion.
///
/// Chord precedence: longer modifier combinations (Ctrl+Shift+S) are checked
/// before shorter ones (Ctrl+S) so the correct shortcut always fires.
/// </summary>
public class InputRoutingService
{
    /// <summary>Application state scope for keyboard shortcut routing.</summary>
    public enum InputScope
    {
        /// <summary>Normal playback (default).</summary>
        Normal = 0,
        /// <summary>A modal dialog is currently open.</summary>
        DialogOpen = 1,
        /// <summary>Fullscreen mode.</summary>
        Fullscreen = 2,
        /// <summary>Picture-in-Picture is active.</summary>
        PipActive = 3,
        /// <summary>Text editing control has focus (TextBox, etc.).</summary>
        TextEdit = 4,
    }

    private readonly List<RegisteredShortcut> _bindings = new();
    private readonly Stack<InputScope> _scopeStack = new();
    private readonly object _lock = new();

    public InputRoutingService()
    {
        // Default scope is Normal
        _scopeStack.Push(InputScope.Normal);
    }

    /// <summary>
    /// The current effective scope (top of the scope stack).
    /// </summary>
    public InputScope CurrentScope
    {
        get
        {
            lock (_lock)
            {
                return _scopeStack.Count > 0 ? _scopeStack.Peek() : InputScope.Normal;
            }
        }
    }

    /// <summary>
    /// Number of scopes currently on the stack (useful for debugging nested dialogs).
    /// </summary>
    public int ScopeDepth
    {
        get
        {
            lock (_lock) { return _scopeStack.Count; }
        }
    }

    /// <summary>
    /// Push a new scope onto the stack (e.g., when a dialog opens).
    /// Call <see cref="PopScope"/> when the scope ends.
    /// </summary>
    public void PushScope(InputScope scope)
    {
        lock (_lock)
        {
            _scopeStack.Push(scope);
        }
    }

    /// <summary>
    /// Pop the current scope from the stack.
    /// </summary>
    public void PopScope()
    {
        lock (_lock)
        {
            if (_scopeStack.Count > 1) // Never pop the last Normal scope
                _scopeStack.Pop();
        }
    }

    /// <summary>
    /// Clear the scope stack back to Normal. Call when a bulk reset is needed
    /// (e.g., all dialogs closed at once).
    /// </summary>
    public void ClearScopes()
    {
        lock (_lock)
        {
            _scopeStack.Clear();
            _scopeStack.Push(InputScope.Normal);
        }
    }

    /// <summary>
    /// Register a keyboard shortcut.
    /// </summary>
    /// <param name="modifiers">Required modifier keys (Ctrl, Shift, Alt, etc.)</param>
    /// <param name="key">The key that triggers the shortcut.</param>
    /// <param name="action">Action to invoke when the shortcut is triggered.</param>
    /// <param name="description">Human-readable description (for keyboard shortcut dialog).</param>
    /// <param name="scope">Scope(s) in which this shortcut is active (default: Normal).</param>
    public void Register(KeyModifiers modifiers, Key key, Action action,
        string description, InputScope scope = InputScope.Normal)
    {
        var shortcut = new RegisteredShortcut(modifiers, key, action, description, scope);

        lock (_lock)
        {
            // Replace if same combo exists (last registration wins)
            var existing = _bindings.FindIndex(b => b.Key == key && b.Modifiers == modifiers);
            if (existing >= 0)
                _bindings[existing] = shortcut;
            else
                _bindings.Add(shortcut);
        }
    }

    /// <summary>
    /// Attempt to handle a key event, using the current scope stack.
    /// Returns true if a shortcut consumed the event.
    /// </summary>
    public bool TryHandle(KeyEventArgs e)
    {
        return TryHandle(e.Key, e.KeyModifiers);
    }

    /// <summary>
    /// Attempt to handle a key combination directly, using the current scope stack.
    /// </summary>
    public bool TryHandle(Key key, KeyModifiers modifiers)
    {
        RegisteredShortcut? match = null;

        lock (_lock)
        {
            var currentScope = _scopeStack.Count > 0 ? _scopeStack.Peek() : InputScope.Normal;

            // Sort by modifier count descending so longer chords match first
            var candidates = _bindings
                .Where(b => b.Key == key)
                .OrderByDescending(b => CountModifiers(b.Modifiers))
                .ToList();

            foreach (var candidate in candidates)
            {
                // Exact modifier match required
                if (candidate.Modifiers != (modifiers & candidate.Modifiers)) continue;
                // Check that no EXTRA modifiers are pressed
                if ((modifiers & ~candidate.Modifiers) != KeyModifiers.None) continue;
                // Scope check against current scope
                if (!IsScopeActive(candidate.Scope, currentScope)) continue;

                match = candidate;
                break;
            }
        }

        if (match == null)
            return false;

        match.Action();
        return true;
    }

    /// <summary>
    /// Attempt to handle a key combination against an explicit scope (used by tests).
    /// </summary>
    public bool TryHandle(Key key, KeyModifiers modifiers, InputScope scope)
    {
        RegisteredShortcut? match = null;

        lock (_lock)
        {
            var currentScope = scope;

            // Sort by modifier count descending so longer chords match first
            var candidates = _bindings
                .Where(b => b.Key == key)
                .OrderByDescending(b => CountModifiers(b.Modifiers))
                .ToList();

            foreach (var candidate in candidates)
            {
                if (candidate.Modifiers != (modifiers & candidate.Modifiers)) continue;
                if ((modifiers & ~candidate.Modifiers) != KeyModifiers.None) continue;
                if (!IsScopeActive(candidate.Scope, currentScope)) continue;

                match = candidate;
                break;
            }
        }

        if (match == null)
            return false;

        match.Action();
        return true;
    }

    /// <summary>
    /// Returns all registered shortcuts for display in a keyboard shortcuts dialog.
    /// </summary>
    public IReadOnlyList<RegisteredShortcut> GetAllBindings()
    {
        lock (_lock)
        {
            return _bindings.OrderBy(b => b.Description).ToList();
        }
    }

    /// <summary>
    /// Check if a scope is active. DialogOpen overrides Normal and Fullscreen.
    /// TextEdit overrides everything except DialogOpen. PipActive is exclusive.
    /// </summary>
    private static bool IsScopeActive(InputScope registeredScope, InputScope currentScope)
    {
        // Normal scope shortcuts are always available unless overridden
        if (registeredScope == InputScope.Normal)
            return currentScope != InputScope.PipActive;

        // DialogOpen scope: only DialogOpen-registered shortcuts pass through
        if (currentScope == InputScope.DialogOpen)
            return registeredScope == InputScope.DialogOpen;

        // TextEdit scope: only TextEdit-registered shortcuts pass through
        if (currentScope == InputScope.TextEdit)
            return registeredScope == InputScope.TextEdit;

        // PipActive: only PipActive-registered shortcuts pass through
        if (currentScope == InputScope.PipActive)
            return registeredScope == InputScope.PipActive;

        // Exact scope match for all other cases
        return registeredScope == currentScope;
    }

    private static int CountModifiers(KeyModifiers modifiers)
    {
        int count = 0;
        if ((modifiers & KeyModifiers.Control) != 0) count++;
        if ((modifiers & KeyModifiers.Shift) != 0) count++;
        if ((modifiers & KeyModifiers.Alt) != 0) count++;
        return count;
    }
}

/// <summary>
/// A registered keyboard shortcut binding.
/// </summary>
public class RegisteredShortcut
{
    public KeyModifiers Modifiers { get; }
    public Key Key { get; }
    public Action Action { get; }
    public string Description { get; }
    public InputRoutingService.InputScope Scope { get; }

    public RegisteredShortcut(KeyModifiers modifiers, Key key, Action action,
        string description, InputRoutingService.InputScope scope)
    {
        Modifiers = modifiers;
        Key = key;
        Action = action;
        Description = description;
        Scope = scope;
    }

    /// <summary>
    /// Human-readable key combination string (e.g., "Ctrl+Shift+S").
    /// </summary>
    public string GestureText
    {
        get
        {
            var parts = new List<string>();
            if ((Modifiers & KeyModifiers.Control) != 0) parts.Add("Ctrl");
            if ((Modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((Modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            parts.Add(KeyToString());
            return string.Join("+", parts);
        }
    }

    private string KeyToString() => Key switch
    {
        Key.OemPlus or Key.Add => "+",
        Key.OemMinus or Key.Subtract => "-",
        Key.OemComma => ",",
        Key.OemPeriod => ".",
        Key.OemOpenBrackets => "[",
        Key.OemCloseBrackets => "]",
        Key.OemQuestion => "?",
        Key.D0 => "0",
        Key.D1 => "1",
        Key.D2 => "2",
        Key.D3 => "3",
        Key.D4 => "4",
        Key.D5 => "5",
        Key.D6 => "6",
        Key.D7 => "7",
        Key.D8 => "8",
        Key.D9 => "9",
        Key.VolumeUp => "VolumeUp",
        Key.VolumeDown => "VolumeDown",
        Key.VolumeMute => "VolumeMute",
        Key.MediaPlayPause => "MediaPlayPause",
        Key.MediaStop => "MediaStop",
        Key.MediaNextTrack => "MediaNextTrack",
        Key.MediaPreviousTrack => "MediaPreviousTrack",
        Key.PageUp => "PageUp",
        Key.PageDown => "PageDown",
        Key.Space => "Space",
        Key.Back => "Back",
        Key.Escape => "Esc",
        _ => Key.ToString()
    };

    public override string ToString() => $"{GestureText} — {Description}";
}
