namespace Lopen.Core.Testing;

/// <summary>
/// Mock implementation of IInteractiveTestSelector for testing.
/// </summary>
public sealed class MockInteractiveTestSelector : IInteractiveTestSelector
{
    private List<string>? _testIdsToSelect;
    private string? _modelToSelect;
    private bool _cancelled;
    
    /// <summary>Tracks calls to SelectTests.</summary>
    public List<SelectTestsCall> Calls { get; } = new();
    
    /// <summary>
    /// Configure the selector to return specific tests by ID.
    /// </summary>
    /// <param name="testIds">Test IDs to select.</param>
    public MockInteractiveTestSelector WithSelectedTests(params string[] testIds)
    {
        _testIdsToSelect = testIds.ToList();
        return this;
    }
    
    /// <summary>
    /// Configure the selector to return a specific model.
    /// </summary>
    /// <param name="model">Model to return.</param>
    public MockInteractiveTestSelector WithModel(string model)
    {
        _modelToSelect = model;
        return this;
    }
    
    /// <summary>
    /// Configure the selector to indicate cancellation.
    /// </summary>
    public MockInteractiveTestSelector WithCancellation()
    {
        _cancelled = true;
        return this;
    }
    
    /// <inheritdoc />
    public InteractiveTestSelection SelectTests(
        IEnumerable<ITestCase> availableTests,
        string defaultModel,
        CancellationToken cancellationToken = default)
    {
        var testList = availableTests.ToList();
        
        Calls.Add(new SelectTestsCall
        {
            AvailableTests = testList,
            DefaultModel = defaultModel
        });
        
        if (_cancelled)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        // If specific tests configured, filter to those
        IReadOnlyList<ITestCase> selectedTests;
        if (_testIdsToSelect != null)
        {
            selectedTests = testList
                .Where(t => _testIdsToSelect.Contains(t.TestId))
                .ToList();
        }
        else
        {
            // Default: select all tests
            selectedTests = testList;
        }
        
        return new InteractiveTestSelection
        {
            Tests = selectedTests,
            Model = _modelToSelect ?? defaultModel,
            Cancelled = false
        };
    }
    
    /// <summary>
    /// Record of a call to SelectTests.
    /// </summary>
    public sealed record SelectTestsCall
    {
        /// <summary>Available tests passed to the selector.</summary>
        public required IReadOnlyList<ITestCase> AvailableTests { get; init; }
        
        /// <summary>Default model passed to the selector.</summary>
        public required string DefaultModel { get; init; }
    }
}
