namespace Lopen.Core;

/// <summary>
/// Provides ASCII art logos for different terminal widths.
/// Features the Wind Runner radiant order sigil.
/// </summary>
public class AsciiLogoProvider
{
    /// <summary>
    /// Gets the appropriate logo for the available terminal width.
    /// </summary>
    /// <param name="availableWidth">Terminal width in characters.</param>
    /// <returns>ASCII art logo string.</returns>
    public string GetLogo(int availableWidth) => availableWidth switch
    {
        >= 80 => GetFullLogo(),
        >= 50 => GetCompactLogo(),
        _ => GetMinimalLogo()
    };

    /// <summary>
    /// Full ASCII art logo for wide terminals (80+ chars).
    /// Wind Runner sigil design.
    /// </summary>
    public static string GetFullLogo() => string.Join("\n",
        "      ⚡ Wind Runner ⚡",
        "",
        "         ▄▄▄▄▄▄▄▄▄",
        "      ▄▀▀         ▀▀▄",
        "    ▄▀   ▄▄▄▄▄▄▄    ▀▄",
        "   █   ▄▀▀     ▀▀▄   █",
        "  █   █    ⚡    █   █",
        "   █   ▀▄▄     ▄▄▀   █",
        "    ▀▄    ▀▀▀▀▀    ▄▀",
        "      ▀▄▄       ▄▄▀",
        "         ▀▀▀▀▀▀▀");

    /// <summary>
    /// Compact logo for medium terminals (50-79 chars).
    /// </summary>
    public static string GetCompactLogo() => "⚡ lopen ⚡";

    /// <summary>
    /// Minimal text for narrow terminals (&lt;50 chars).
    /// </summary>
    public static string GetMinimalLogo() => "lopen";

    /// <summary>
    /// Gets the tagline text.
    /// </summary>
    public static string GetTagline() => "Interactive Copilot Agent Loop";

    /// <summary>
    /// Gets the help tip text.
    /// </summary>
    public static string GetHelpTip() => "Type 'help' or 'lopen --help' for commands";
}
