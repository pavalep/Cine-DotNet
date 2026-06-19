using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.Services;

public class ErrorBoundaryTests
{
    // ── Sync Run ─────────────────────────────────────────────────

    [Fact]
    public void Run_Action_ExecutesSuccessfully()
    {
        var executed = false;
        ErrorBoundary.Run(() => { executed = true; });
        executed.ShouldBeTrue();
    }

    [Fact]
    public void Run_Action_HandlesException()
    {
        Exception? captured = null;
        ErrorBoundary.Run(
            () => throw new InvalidOperationException("test error"),
            ex => captured = ex);

        captured.ShouldNotBeNull();
        captured.Message.ShouldBe("test error");
    }

    [Fact]
    public void Run_Action_DoesNotThrow()
    {
        Should.NotThrow(() =>
            ErrorBoundary.Run(() => throw new InvalidOperationException("test error")));
    }

    // ── Async Run ────────────────────────────────────────────────

    [Fact]
    public async Task Run_AsyncAction_ExecutesSuccessfully()
    {
        var executed = false;
        ErrorBoundary.Run(async () =>
        {
            await Task.Yield();
            executed = true;
        });
        await Task.Delay(100);
        executed.ShouldBeTrue();
    }

    [Fact]
    public void Run_AsyncAction_HandlesException()
    {
        Exception? captured = null;

        ErrorBoundary.Run(
            async () =>
            {
                await Task.Yield();
                throw new InvalidOperationException("async error");
            },
            ex => captured = ex);

        // Give the async void time to complete
        Should.NotThrow(() =>
            Task.Delay(500).Wait());
        captured.ShouldNotBeNull();
        captured.Message.ShouldBe("async error");
    }

    // ── TryAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task TryAsync_Success_ReturnsOk()
    {
        var result = await ErrorBoundary.TryAsync(async () =>
        {
            await Task.Yield();
        });

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task TryAsync_Failure_ReturnsFail()
    {
        var result = await ErrorBoundary.TryAsync(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("fail");
        });

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe("fail");
    }
}
