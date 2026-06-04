using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Typed property watcher that replaces PropertyChanged switch-case with lambdas.
/// P8.3: Compile-time safe, no magic strings.
/// Usage: watcher.Watch(() => vm.VolumeValue, v => ShowOsdNotification($"Volume: {v}%"));
/// </summary>
public class PropertyWatcher : IDisposable
{
    private readonly INotifyPropertyChanged _source;
    private readonly List<(string Name, Delegate Handler)> _watches = new();

    public PropertyWatcher(INotifyPropertyChanged source)
    {
        _source = source;
        _source.PropertyChanged += OnPropertyChanged;
    }

    /// <summary>
    /// Watch a property by lambda expression. Fires callback with typed value.
    /// </summary>
    public PropertyWatcher Watch<T>(Expression<Func<T>> propertyExpr, Action<T> onChange)
    {
        var name = GetPropertyName(propertyExpr);
        _watches.Add((name, onChange));
        return this;
    }

    /// <summary>
    /// Watch a property by name (for when expression trees aren't possible).
    /// </summary>
    public PropertyWatcher Watch(string propertyName, Action onChange)
    {
        _watches.Add((propertyName, onChange));
        return this;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        foreach (var (name, handler) in _watches)
        {
            if (name != e.PropertyName) continue;

            if (handler is Action action)
            {
                action();
            }
            else if (handler is Delegate d)
            {
                // For typed watches, we need to get the property value via reflection
                var prop = _source.GetType().GetProperty(name);
                if (prop != null)
                {
                    var value = prop.GetValue(_source);
                    d.DynamicInvoke(value);
                }
            }
        }
    }

    public void Dispose()
    {
        _source.PropertyChanged -= OnPropertyChanged;
        _watches.Clear();
    }

    private static string GetPropertyName<T>(Expression<Func<T>> expr) =>
        expr.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression u when u.Operand is MemberExpression m => m.Member.Name,
            _ => throw new ArgumentException("Expression must be a property access.")
        };
}
