using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Color categories for adaptive color selection.
/// </summary>
public enum ColorCategory
{
    /// <summary>Success, completed operations (green)</summary>
    Success,
    
    /// <summary>Error, failure (red)</summary>
    Error,
    
    /// <summary>Warning, caution (yellow)</summary>
    Warning,
    
    /// <summary>Information (blue)</summary>
    Info,
    
    /// <summary>Secondary, muted text (gray)</summary>
    Muted,
    
    /// <summary>Highlighted/emphasized content (cyan)</summary>
    Highlight,
    
    /// <summary>Accent, special markers (magenta)</summary>
    Accent
}

/// <summary>
/// Provides adaptive color selection based on terminal capabilities.
/// </summary>
public interface IColorProvider
{
    /// <summary>
    /// Gets the best available color for a category based on terminal capabilities.
    /// </summary>
    /// <param name="category">The color category.</param>
    /// <returns>A Spectre.Console Color appropriate for the terminal.</returns>
    Color GetColor(ColorCategory category);
}

/// <summary>
/// Provides adaptive colors based on terminal color depth capabilities.
/// Gracefully degrades from TrueColor → 256 → 16 colors.
/// </summary>
public class ColorProvider : IColorProvider
{
    private readonly ITerminalCapabilities _capabilities;

    /// <summary>
    /// Creates a new color provider with the specified capabilities.
    /// </summary>
    /// <param name="capabilities">Terminal capabilities to use for color selection.</param>
    public ColorProvider(ITerminalCapabilities capabilities)
    {
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    /// <inheritdoc />
    public Color GetColor(ColorCategory category)
    {
        if (!_capabilities.SupportsColor)
        {
            return Color.Default;
        }

        return category switch
        {
            ColorCategory.Success => GetSuccessColor(),
            ColorCategory.Error => GetErrorColor(),
            ColorCategory.Warning => GetWarningColor(),
            ColorCategory.Info => GetInfoColor(),
            ColorCategory.Muted => GetMutedColor(),
            ColorCategory.Highlight => GetHighlightColor(),
            ColorCategory.Accent => GetAccentColor(),
            _ => Color.Default
        };
    }

    private Color GetSuccessColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(0, 255, 0);  // Bright green RGB
        if (_capabilities.Supports256Colors)
            return Color.Green;  // 256-color green
        return Color.Green;  // Standard green
    }

    private Color GetErrorColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(255, 0, 0);  // Bright red RGB
        if (_capabilities.Supports256Colors)
            return Color.Red;
        return Color.Red;
    }

    private Color GetWarningColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(255, 255, 0);  // Yellow RGB
        if (_capabilities.Supports256Colors)
            return Color.Yellow;
        return Color.Yellow;
    }

    private Color GetInfoColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(0, 153, 255);  // Bright blue RGB
        if (_capabilities.Supports256Colors)
            return Color.Blue;
        return Color.Blue;
    }

    private Color GetMutedColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(128, 128, 128);  // Gray RGB
        if (_capabilities.Supports256Colors)
            return Color.Grey;
        return Color.Grey;
    }

    private Color GetHighlightColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(0, 255, 255);  // Cyan RGB
        if (_capabilities.Supports256Colors)
            return Color.Cyan1;
        return Color.Aqua;
    }

    private Color GetAccentColor()
    {
        if (_capabilities.SupportsTrueColor)
            return new Color(255, 0, 255);  // Magenta RGB
        if (_capabilities.Supports256Colors)
            return Color.Magenta1;
        return Color.Fuchsia;
    }
}
