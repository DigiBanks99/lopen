namespace Lopen.Core;

/// <summary>
/// Types of status symbols used for visual feedback.
/// </summary>
public enum StatusSymbol
{
    /// <summary>✓ Success, completed</summary>
    Success,
    
    /// <summary>✗ Error, failed</summary>
    Error,
    
    /// <summary>⚠ Warning, caution</summary>
    Warning,
    
    /// <summary>ℹ Information</summary>
    Info,
    
    /// <summary>⏳ In progress</summary>
    Progress,
    
    /// <summary>✨ New, special</summary>
    New,
    
    /// <summary>🚀 Launch, start</summary>
    Launch,
    
    /// <summary>⚡ Fast, important</summary>
    Fast,
    
    /// <summary>💡 Tip, suggestion</summary>
    Tip
}

/// <summary>
/// Provides adaptive status symbols based on terminal capabilities.
/// </summary>
public interface ISymbolProvider
{
    /// <summary>
    /// Gets the appropriate symbol for the given status type.
    /// </summary>
    /// <param name="symbol">The type of symbol to get.</param>
    /// <returns>The symbol string (unicode or ASCII fallback).</returns>
    string GetSymbol(StatusSymbol symbol);
}

/// <summary>
/// Provides adaptive status symbols with unicode/ASCII fallback based on terminal capabilities.
/// </summary>
public class SymbolProvider : ISymbolProvider
{
    private readonly bool _supportsUnicode;

    /// <summary>
    /// Creates a new symbol provider with unicode support detection.
    /// </summary>
    /// <param name="supportsUnicode">Whether the terminal supports unicode.</param>
    public SymbolProvider(bool supportsUnicode)
    {
        _supportsUnicode = supportsUnicode;
    }

    /// <summary>
    /// Creates a new symbol provider using terminal capabilities.
    /// </summary>
    /// <param name="capabilities">Terminal capabilities to use.</param>
    public SymbolProvider(ITerminalCapabilities capabilities)
        : this(capabilities?.SupportsUnicode ?? false)
    {
    }

    /// <inheritdoc />
    public string GetSymbol(StatusSymbol symbol)
    {
        return symbol switch
        {
            StatusSymbol.Success => "✓",   // U+2713 - works everywhere
            StatusSymbol.Error => "✗",     // U+2717 - works everywhere
            StatusSymbol.Warning => _supportsUnicode ? "⚠" : "!",   // U+26A0
            StatusSymbol.Info => _supportsUnicode ? "ℹ" : "[i]",    // U+2139
            StatusSymbol.Progress => _supportsUnicode ? "⏳" : "...", // U+23F3
            StatusSymbol.New => _supportsUnicode ? "✨" : "*",       // U+2728
            StatusSymbol.Launch => _supportsUnicode ? "🚀" : ">>",   // U+1F680
            StatusSymbol.Fast => _supportsUnicode ? "⚡" : "!",      // U+26A1
            StatusSymbol.Tip => _supportsUnicode ? "💡" : "?",       // U+1F4A1
            _ => "•"
        };
    }
}
