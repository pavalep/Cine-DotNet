using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Simba.Avalonia.Core;

/// <summary>
/// In-memory typed event bus for decoupled pub/sub.
/// Thread-safe — handlers are invoked synchronously on the publisher's thread.
/// </summary>
public sealed class EventBus : IEventBus, IDisposable
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private bool _disposed;

    public void Publish<T>(T @event) where T : class
    {
        if (_disposed) return;
        if (@event == null) throw new ArgumentNullException(nameof(@event));

        if (_handlers.TryGetValue(typeof(T), out var handlers))
        {
            // Snapshot to avoid mutation during iteration
            var snapshot = handlers.ToArray();
            foreach (var handler in snapshot)
            {
                if (handler is Action<T> action)
                    action(@event);
            }
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : class
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EventBus));
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        var type = typeof(T);
        _handlers.AddOrUpdate(
            type,
            _ => new List<Delegate> { handler },
            (_, list) =>
            {
                lock (list) { list.Add(handler); }
                return list;
            });

        return new Unsubscriber<T>(this, handler);
    }

    public void Dispose()
    {
        _disposed = true;
        _handlers.Clear();
    }

    private void Unsubscribe<T>(Action<T> handler) where T : class
    {
        if (_handlers.TryGetValue(typeof(T), out var list))
        {
            lock (list) { list.Remove(handler); }
        }
    }

    private sealed class Unsubscriber<T> : IDisposable where T : class
    {
        private readonly EventBus _bus;
        private readonly Action<T> _handler;
        public Unsubscriber(EventBus bus, Action<T> handler) { _bus = bus; _handler = handler; }
        public void Dispose() => _bus.Unsubscribe(_handler);
    }
}
