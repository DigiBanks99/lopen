namespace Lopen.Core;

/// <summary>
/// Mock implementation of welcome header renderer for testing.
/// Records all render calls for verification.
/// </summary>
public class MockWelcomeHeaderRenderer : IWelcomeHeaderRenderer
{
    private readonly List<WelcomeHeaderContext> _renderCalls = new();

    /// <summary>
    /// Gets all render calls made.
    /// </summary>
    public IReadOnlyList<WelcomeHeaderContext> RenderCalls => _renderCalls.AsReadOnly();

    /// <summary>
    /// Gets the last context rendered.
    /// </summary>
    public WelcomeHeaderContext? LastContext => _renderCalls.Count > 0 ? _renderCalls[^1] : null;

    /// <summary>
    /// Gets whether RenderWelcomeHeader was called.
    /// </summary>
    public bool WasCalled => _renderCalls.Count > 0;

    /// <inheritdoc />
    public void RenderWelcomeHeader(WelcomeHeaderContext context)
    {
        _renderCalls.Add(context);
    }

    /// <summary>
    /// Clears all recorded calls.
    /// </summary>
    public void Reset()
    {
        _renderCalls.Clear();
    }
}
