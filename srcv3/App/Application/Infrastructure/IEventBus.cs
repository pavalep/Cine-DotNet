using System;

namespace Cine.Avalonia.Infrastructure;

/// <summary>
/// Simple typed event bus for decoupled pub/sub communication between
/// domain managers and the shell/UI layer.
/// </summary>
public interface IEventBus
{
    /// <summary>Publish an event to all registered handlers.</summary>
    void Publish<T>(T @event) where T : class;

    /// <summary>Subscribe a handler for events of type <typeparamref name="T"/>.</summary>
    /// <returns>A disposable that unsubscribes the handler when disposed.</returns>
    IDisposable Subscribe<T>(Action<T> handler) where T : class;
}
