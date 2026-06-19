using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using Xunit;

namespace Cine.Tests.Infrastructure;

/// <summary>
/// Initialises the Avalonia headless platform once per test collection.
/// Uses <see cref="HeadlessUnitTestSession"/> for manual headless setup
/// without requiring the XUnit integration package (which conflicts with xunit v2).
/// </summary>
public sealed class HeadlessFixture : IAsyncLifetime
{
    private HeadlessUnitTestSession? _session;

    public Task InitializeAsync()
    {
        if (_session != null) return Task.CompletedTask;

        _session = HeadlessUnitTestSession.StartNew(typeof(Cine.Avalonia.App));
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_session != null)
        {
            await _session.DisposeAsync();
            _session = null;
        }
    }

    /// <summary>
    /// Run an action on the headless UI thread.
    /// </summary>
    public Task DispatchAsync(Action action)
    {
        if (_session == null)
            throw new InvalidOperationException("Headless session not initialized.");

        return _session.Dispatch(action, CancellationToken.None);
    }
}

/// <summary>
/// xUnit collection definition for headless Avalonia tests.
/// All headless tests must be in this collection to share the single headless session.
/// </summary>
[CollectionDefinition("Headless")]
public class HeadlessCollection : ICollectionFixture<HeadlessFixture>
{
}
