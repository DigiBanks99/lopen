namespace Lopen.Core.Testing;

/// <summary>
/// Mode for keyword matching.
/// </summary>
public enum MatchMode
{
    /// <summary>Pass if any keyword is found.</summary>
    Any,
    
    /// <summary>Pass only if all keywords are found.</summary>
    All
}

/// <summary>
/// Validates responses by checking for presence of keywords.
/// </summary>
public sealed class KeywordValidator : ITestValidator
{
    private readonly IReadOnlyList<string> _keywords;
    private readonly MatchMode _matchMode;
    
    /// <summary>
    /// Creates a keyword validator.
    /// </summary>
    /// <param name="keywords">Keywords to search for in the response.</param>
    /// <param name="matchMode">Whether to match any or all keywords.</param>
    public KeywordValidator(IEnumerable<string> keywords, MatchMode matchMode = MatchMode.Any)
    {
        _keywords = keywords.ToList();
        _matchMode = matchMode;
    }
    
    /// <summary>
    /// Creates a keyword validator for matching any of the provided keywords.
    /// </summary>
    public KeywordValidator(params string[] keywords)
        : this(keywords, MatchMode.Any)
    {
    }
    
    /// <inheritdoc/>
    public ValidationResult Validate(string response)
    {
        if (string.IsNullOrEmpty(response) || _keywords.Count == 0)
        {
            return new ValidationResult(false);
        }
        
        var matches = new List<string>();
        
        foreach (var keyword in _keywords)
        {
            if (response.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                if (_matchMode == MatchMode.Any)
                {
                    // Return immediately for "any" mode
                    return new ValidationResult(true, keyword);
                }
                matches.Add(keyword);
            }
        }
        
        // For "all" mode, check if all keywords were matched
        if (_matchMode == MatchMode.All && matches.Count == _keywords.Count)
        {
            return new ValidationResult(true, string.Join(", ", matches));
        }
        
        return new ValidationResult(false);
    }
}
