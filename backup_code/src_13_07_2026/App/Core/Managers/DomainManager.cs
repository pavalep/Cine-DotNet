using System;
using Cine.Media.Interfaces;

namespace Cine.Avalonia.Core;

/// <summary>
/// Base class for domain managers that wrap a media player interface.
/// Provides disposal pattern and a strongly typed Player property.
/// </summary>
/// <typeparam name="T">The player role interface this manager operates on.</typeparam>
public abstract class DomainManager<T> : IDisposable where T : class
{
    private bool _disposed;

    protected T Player { get; }

    /// <summary>True after Dispose has been called.</summary>
    protected bool IsDisposed => _disposed;

    protected DomainManager(T player)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeCore();
    }

    protected virtual void DisposeCore() { }

    protected void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }
}
