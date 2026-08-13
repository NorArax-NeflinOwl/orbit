using Microsoft.JSInterop;

namespace Orbit.Web.Tests.TestDoubles;

/// <summary>
/// Stands in for the browser's localStorage during tests: <see cref="Orbit.Web.Services.TokenStore"/>
/// calls "localStorage.getItem"/"setItem"/"removeItem" through <see cref="IJSRuntime"/>, so this fake
/// intercepts exactly those calls against an in-memory dictionary instead of talking to a real browser.
/// </summary>
internal sealed class StubJSRuntime : IJSRuntime
{
    private readonly Dictionary<string, string> _localStorage = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = (string)args![0]!;
        object? result = identifier switch
        {
            "localStorage.getItem" => _localStorage.GetValueOrDefault(key),
            "localStorage.setItem" => SetItem(key, (string)args[1]!),
            "localStorage.removeItem" => RemoveItem(key),
            _ => throw new NotSupportedException($"StubJSRuntime does not support '{identifier}'.")
        };

        return ValueTask.FromResult((TValue)result!);
    }

    private object? SetItem(string key, string value)
    {
        _localStorage[key] = value;
        return null;
    }

    private object? RemoveItem(string key)
    {
        _localStorage.Remove(key);
        return null;
    }
}
