using Microsoft.Extensions.Options;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="IOptionsMonitor{TOptions}"/> stub that always returns a fixed value. The health
/// checks under test only ever read <c>CurrentValue</c>, so there is no need to simulate reload
/// notifications through <see cref="OnChange"/>.
/// </summary>
internal sealed class TestOptionsMonitor<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
{
    public TOptions CurrentValue { get; } = currentValue;

    public TOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<TOptions, string?> listener) => NoopDisposable.Instance;

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
