using Avalonia.Input;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class InputRoutingServiceTests
{
    private readonly InputRoutingService _sut = new();

    // ── Basic Registration ──────────────────────────────────────

    [Fact]
    public void RegisterSingleKey_HandlesCorrectKey()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.A, () => triggered = true, "Test");

        var result = _sut.TryHandle(Key.A, KeyModifiers.None);

        result.ShouldBeTrue();
        triggered.ShouldBeTrue();
    }

    [Fact]
    public void RegisterSingleKey_WrongKey_ReturnsFalse()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.A, () => triggered = true, "Test");

        var result = _sut.TryHandle(Key.B, KeyModifiers.None);

        result.ShouldBeFalse();
        triggered.ShouldBeFalse();
    }

    [Fact]
    public void RegisterSingleKey_WrongModifiers_ReturnsFalse()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.Control, Key.S, () => triggered = true, "Test");

        // Try with no modifiers — should NOT trigger Ctrl+S shortcut
        var result = _sut.TryHandle(Key.S, KeyModifiers.None);

        result.ShouldBeFalse();
        triggered.ShouldBeFalse();
    }

    // ── Modifier Combinations ────────────────────────────────────

    [Fact]
    public void RegisterCtrlShiftChord_HandlesCorrectCombo()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.Control | KeyModifiers.Shift, Key.S,
            () => triggered = true, "Ctrl+Shift+S");

        var result = _sut.TryHandle(Key.S,
            KeyModifiers.Control | KeyModifiers.Shift);

        result.ShouldBeTrue();
        triggered.ShouldBeTrue();
    }

    [Fact]
    public void CtrlShiftChord_NotTriggeredByCtrlOnly()
    {
        var ctrlShiftTriggered = false;
        var ctrlTriggered = false;

        _sut.Register(KeyModifiers.Control | KeyModifiers.Shift, Key.S,
            () => ctrlShiftTriggered = true, "Ctrl+Shift+S");
        _sut.Register(KeyModifiers.Control, Key.S,
            () => ctrlTriggered = true, "Ctrl+S");

        var result = _sut.TryHandle(Key.S, KeyModifiers.Control);

        result.ShouldBeTrue();
        ctrlTriggered.ShouldBeTrue();
        ctrlShiftTriggered.ShouldBeFalse(); // Ctrl+S, not Ctrl+Shift+S
    }

    [Fact]
    public void CtrlShiftChord_CheckedBeforeCtrl()
    {
        var ctrlShiftTriggered = false;
        var ctrlTriggered = false;

        _sut.Register(KeyModifiers.Control, Key.S,
            () => ctrlTriggered = true, "Ctrl+S");
        _sut.Register(KeyModifiers.Control | KeyModifiers.Shift, Key.S,
            () => ctrlShiftTriggered = true, "Ctrl+Shift+S");

        var result = _sut.TryHandle(Key.S,
            KeyModifiers.Control | KeyModifiers.Shift);

        result.ShouldBeTrue();
        ctrlShiftTriggered.ShouldBeTrue(); // longer chord wins
        ctrlTriggered.ShouldBeFalse();
    }

    [Fact]
    public void ExtraModifiersPressed_NoMatch()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.Control, Key.S, () => triggered = true, "Ctrl+S");

        // Ctrl+Shift+S (extra Shift) should NOT trigger Ctrl+S
        var result = _sut.TryHandle(Key.S,
            KeyModifiers.Control | KeyModifiers.Shift);

        result.ShouldBeFalse();
        triggered.ShouldBeFalse();
    }

    // ── Scopes ──────────────────────────────────────────────────

    [Fact]
    public void NormalScope_FiresInNormalScope()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.Space, () => triggered = true, "Play",
            InputRoutingService.InputScope.Normal);

        _sut.TryHandle(Key.Space, KeyModifiers.None,
            InputRoutingService.InputScope.Normal);

        triggered.ShouldBeTrue();
    }

    [Fact]
    public void NormalScope_FiresInDialogScope()
    {
        // Normal-scope shortcuts always fire — scope blocking is for extras
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.Space, () => triggered = true, "Play",
            InputRoutingService.InputScope.Normal);

        _sut.TryHandle(Key.Space, KeyModifiers.None,
            InputRoutingService.InputScope.DialogOpen);

        triggered.ShouldBeTrue();
    }

    [Fact]
    public void FullscreenScopeOnly_FiresInFullscreen()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.F, () => triggered = true, "ToggleFullscreen",
            InputRoutingService.InputScope.Fullscreen);

        _sut.TryHandle(Key.F, KeyModifiers.None,
            InputRoutingService.InputScope.Fullscreen);

        triggered.ShouldBeTrue();
    }

    [Fact]
    public void FullscreenScopeOnly_DoesNotFireInNormal()
    {
        var triggered = false;
        _sut.Register(KeyModifiers.None, Key.F, () => triggered = true, "ToggleFullscreen",
            InputRoutingService.InputScope.Fullscreen);

        var result = _sut.TryHandle(Key.F, KeyModifiers.None,
            InputRoutingService.InputScope.Normal);

        result.ShouldBeFalse();
        triggered.ShouldBeFalse();
    }

    // ── Registration Overwrite ──────────────────────────────────

    [Fact]
    public void RegisterOverwrite_LastWins()
    {
        var firstTriggered = false;
        var secondTriggered = false;

        _sut.Register(KeyModifiers.None, Key.A, () => firstTriggered = true, "First");
        _sut.Register(KeyModifiers.None, Key.A, () => secondTriggered = true, "Second");

        _sut.TryHandle(Key.A, KeyModifiers.None);

        firstTriggered.ShouldBeFalse();
        secondTriggered.ShouldBeTrue();
    }

    // ── GetAllBindings ──────────────────────────────────────────

    [Fact]
    public void GetAllBindings_ReturnsAllRegistered()
    {
        _sut.Register(KeyModifiers.Control, Key.S, () => { }, "Stop");
        _sut.Register(KeyModifiers.None, Key.Space, () => { }, "Play");

        var bindings = _sut.GetAllBindings();

        bindings.Count.ShouldBe(2);
        bindings.Any(b => b.Description == "Stop").ShouldBeTrue();
        bindings.Any(b => b.Description == "Play").ShouldBeTrue();
    }

    [Fact]
    public void GetAllBindings_ReturnsEmptyWhenNone()
    {
        _sut.GetAllBindings().Count.ShouldBe(0);
    }

    // ── GestureText ─────────────────────────────────────────────

    [Fact]
    public void GestureText_PlainKey()
    {
        var shortcut = new RegisteredShortcut(KeyModifiers.None, Key.Space,
            () => { }, "Play", InputRoutingService.InputScope.Normal);

        shortcut.GestureText.ShouldBe("Space");
    }

    [Fact]
    public void GestureText_ControlKey()
    {
        var shortcut = new RegisteredShortcut(KeyModifiers.Control, Key.S,
            () => { }, "Stop", InputRoutingService.InputScope.Normal);

        shortcut.GestureText.ShouldBe("Ctrl+S");
    }

    [Fact]
    public void GestureText_ControlShiftKey()
    {
        var shortcut = new RegisteredShortcut(
            KeyModifiers.Control | KeyModifiers.Shift, Key.P,
            () => { }, "Toggle PIP", InputRoutingService.InputScope.Normal);

        shortcut.GestureText.ShouldBe("Ctrl+Shift+P");
    }

    [Fact]
    public void GestureText_SpecialKey()
    {
        var shortcut = new RegisteredShortcut(KeyModifiers.Control, Key.OemComma,
            () => { }, "Preferences", InputRoutingService.InputScope.Normal);

        shortcut.GestureText.ShouldBe("Ctrl+,");
    }

    // ── Thread Safety ───────────────────────────────────────────

    [Fact]
    public void ConcurrentRegistration_DoesNotThrow()
    {
        _sut.Register(KeyModifiers.None, Key.A, () => { }, "A");

        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            int captured = i;
            tasks[i] = Task.Run(() =>
                _sut.Register(KeyModifiers.None, Key.A, () => { }, $"A{captured}"));
        }

        Should.NotThrow(() => Task.WaitAll(tasks));
    }

    // ── Multiple Shortcuts Different Scopes ─────────────────────

    [Fact]
    public void MultipleShortcuts_DifferentKeys_AllFire()
    {
        var a = false; var b = false; var c = false;
        _sut.Register(KeyModifiers.None, Key.A, () => a = true, "A");
        _sut.Register(KeyModifiers.None, Key.B, () => b = true, "B");
        _sut.Register(KeyModifiers.None, Key.C, () => c = true, "C");

        _sut.TryHandle(Key.A, KeyModifiers.None);
        _sut.TryHandle(Key.B, KeyModifiers.None);
        _sut.TryHandle(Key.C, KeyModifiers.None);

        a.ShouldBeTrue();
        b.ShouldBeTrue();
        c.ShouldBeTrue();
    }
}
