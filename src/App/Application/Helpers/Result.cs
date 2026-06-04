using System;
using System.Diagnostics.CodeAnalysis;

namespace Cine.Avalonia.Helpers;

/// <summary>
/// Discriminated union for operation results — success or failure with error context.
/// P7.2: Replaces silent catch { } patterns with typed error handling.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _isSuccess = true;
    }

    private Result(string error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    [MemberNotNullWhen(true, nameof(_value))]
    [MemberNotNullWhen(false, nameof(_error))]
    public bool IsSuccess => _isSuccess;

    public bool IsFailure => !_isSuccess;

    public T Value => _isSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value of a failed Result: {_error}");

    public string? Error => _isSuccess ? null : _error;

    public T UnwrapOr(T defaultValue) => _isSuccess ? _value! : defaultValue;

    public Result<T> Or(Func<Result<T>> fallback) => _isSuccess ? this : fallback();

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper) =>
        _isSuccess ? Result<TNew>.Ok(mapper(_value!)) : Result<TNew>.Fail(_error!);

    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder) =>
        _isSuccess ? binder(_value!) : Result<TNew>.Fail(_error!);

    public void Match(Action<T> onSuccess, Action<string> onFailure)
    {
        if (_isSuccess) onSuccess(_value!);
        else onFailure(_error!);
    }

    public TUnify Match<TUnify>(Func<T, TUnify> onSuccess, Func<string, TUnify> onFailure) =>
        _isSuccess ? onSuccess(_value!) : onFailure(_error!);

    public static Result<T> Ok(T value) => new(value);
    public static Result<T> Fail(string error) => new(error);
    public static Result<T> From(Func<T> action)
    {
        try { return Ok(action()); }
        catch (Exception ex) { return Fail(ex.Message); }
    }
    public static async Task<Result<T>> FromAsync(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (Exception ex) { return Fail(ex.Message); }
    }

    public override string ToString() => _isSuccess ? $"Ok({_value})" : $"Fail({_error})";
}

/// <summary>
/// Non-generic helper for Result operations that don't return a value (void operations).
/// </summary>
public readonly struct Result
{
    private readonly string? _error;
    private readonly bool _isSuccess;

    private Result(bool success)
    {
        _error = null;
        _isSuccess = true;
    }

    private Result(string error)
    {
        _error = error;
        _isSuccess = false;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;
    public string? Error => _isSuccess ? null : _error;

    public static Result Ok() => new(true);
    public static Result Fail(string error) => new(error);
    public static Result From(Action action)
    {
        try { action(); return Ok(); }
        catch (Exception ex) { return Fail(ex.Message); }
    }
    public static async Task<Result> FromAsync(Func<Task> action)
    {
        try { await action(); return Ok(); }
        catch (Exception ex) { return Fail(ex.Message); }
    }
}
