namespace Lopen.Llm;

/// <summary>
/// No-op session state saver used when no real saver is registered.
/// </summary>
internal sealed class NullSessionStateSaver : ISessionStateSaver
{
    public Task SaveAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
