namespace Lopen.Core.Testing.TestSuites;

/// <summary>
/// Registry of all available test suites.
/// </summary>
public static class TestSuiteRegistry
{
    /// <summary>
    /// Get all available tests from all suites.
    /// </summary>
    public static IEnumerable<ITestCase> GetAllTests()
    {
        foreach (var test in CoreTestSuite.GetTests())
            yield return test;
        
        foreach (var test in AuthTestSuite.GetTests())
            yield return test;
        
        foreach (var test in SessionTestSuite.GetTests())
            yield return test;
        
        foreach (var test in ChatTestSuite.GetTests())
            yield return test;
    }
    
    /// <summary>
    /// Get all available suite names.
    /// </summary>
    public static IEnumerable<string> GetSuiteNames()
    {
        yield return CoreTestSuite.SuiteName;
        yield return AuthTestSuite.SuiteName;
        yield return SessionTestSuite.SuiteName;
        yield return ChatTestSuite.SuiteName;
    }
    
    /// <summary>
    /// Filter tests by a pattern (matches test ID, suite, or description).
    /// </summary>
    /// <param name="pattern">Pattern to match (case-insensitive substring).</param>
    public static IEnumerable<ITestCase> FilterByPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            foreach (var test in GetAllTests())
                yield return test;
            yield break;
        }
        
        foreach (var test in GetAllTests())
        {
            if (test.TestId.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                test.Suite.Contains(pattern, StringComparison.OrdinalIgnoreCase) ||
                test.Description.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                yield return test;
            }
        }
    }
    
    /// <summary>
    /// Get tests by suite name.
    /// </summary>
    /// <param name="suiteName">Name of the suite.</param>
    public static IEnumerable<ITestCase> GetTestsBySuite(string suiteName)
    {
        return suiteName.ToLowerInvariant() switch
        {
            "core" => CoreTestSuite.GetTests(),
            "auth" => AuthTestSuite.GetTests(),
            "session" => SessionTestSuite.GetTests(),
            "chat" => ChatTestSuite.GetTests(),
            _ => Enumerable.Empty<ITestCase>()
        };
    }
}
