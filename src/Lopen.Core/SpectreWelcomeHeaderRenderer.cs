using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Spectre.Console implementation of welcome header renderer.
/// Displays responsive header based on terminal width.
/// Respects NO_COLOR environment variable.
/// </summary>
public class SpectreWelcomeHeaderRenderer : IWelcomeHeaderRenderer
{
    private readonly IAnsiConsole _console;
    private readonly bool _useColors;
    private readonly AsciiLogoProvider _logoProvider;

    public SpectreWelcomeHeaderRenderer()
        : this(AnsiConsole.Console)
    {
    }

    public SpectreWelcomeHeaderRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _useColors = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        _logoProvider = new AsciiLogoProvider();
    }

    /// <inheritdoc />
    public void RenderWelcomeHeader(WelcomeHeaderContext context)
    {
        var width = context.Terminal?.Width ?? _console.Profile.Width;

        if (width >= 80)
        {
            RenderFullHeader(context, width);
        }
        else if (width >= 50)
        {
            RenderCompactHeader(context);
        }
        else
        {
            RenderMinimalHeader(context);
        }
    }

    private void RenderFullHeader(WelcomeHeaderContext context, int width)
    {
        var prefs = context.Preferences;

        // Logo
        if (prefs.ShowLogo)
        {
            var logo = _logoProvider.GetLogo(width);
            if (_useColors)
            {
                _console.MarkupLine($"[cyan]{Markup.Escape(logo)}[/]");
            }
            else
            {
                _console.WriteLine(logo);
            }
        }

        // Version and tagline
        RenderVersionLine(context.Version);
        RenderTagline();

        _console.WriteLine();

        // Info panel with tip, session, context
        if (prefs.ShowTip || prefs.ShowSession || prefs.ShowContext)
        {
            RenderInfoSection(context);
        }

        _console.WriteLine();
    }

    private void RenderCompactHeader(WelcomeHeaderContext context)
    {
        var prefs = context.Preferences;

        // Compact logo line
        if (prefs.ShowLogo && _useColors)
        {
            _console.MarkupLine($"[cyan]⚡[/] [bold]lopen v{Markup.Escape(context.Version)}[/] [cyan]⚡[/]");
        }
        else if (prefs.ShowLogo)
        {
            _console.WriteLine($"lopen v{context.Version}");
        }
        else
        {
            RenderVersionLine(context.Version);
        }

        RenderTagline();

        if (prefs.ShowSession || prefs.ShowContext)
        {
            _console.WriteLine();
            RenderSessionLine(context);
        }

        if (prefs.ShowTip)
        {
            RenderTipLine();
        }

        _console.WriteLine();
    }

    private void RenderMinimalHeader(WelcomeHeaderContext context)
    {
        // Single line version
        _console.WriteLine($"lopen v{context.Version}");

        // Session if available
        if (context.Preferences.ShowSession && !string.IsNullOrEmpty(context.SessionName))
        {
            var sessionDisplay = context.SessionName.Length > 20
                ? "..." + context.SessionName[^17..]
                : context.SessionName;

            var contextText = context.ContextWindow.GetDisplayText();
            _console.WriteLine($"Session: {sessionDisplay} | {contextText}");
        }

        // Minimal tip
        if (context.Preferences.ShowTip)
        {
            _console.WriteLine("Type 'help' for commands");
        }

        _console.WriteLine();
    }

    private void RenderVersionLine(string version)
    {
        if (_useColors)
        {
            _console.MarkupLine($"[bold]lopen v{Markup.Escape(version)}[/]");
        }
        else
        {
            _console.WriteLine($"lopen v{version}");
        }
    }

    private void RenderTagline()
    {
        var tagline = AsciiLogoProvider.GetTagline();
        if (_useColors)
        {
            _console.MarkupLine($"[dim]{Markup.Escape(tagline)}[/]");
        }
        else
        {
            _console.WriteLine(tagline);
        }
    }

    private void RenderTipLine()
    {
        var tip = AsciiLogoProvider.GetHelpTip();
        if (_useColors)
        {
            _console.MarkupLine($"[blue]💡[/] {Markup.Escape(tip)}");
        }
        else
        {
            _console.WriteLine($"[i] {tip}");
        }
    }

    private void RenderSessionLine(WelcomeHeaderContext context)
    {
        var parts = new List<string>();

        if (context.Preferences.ShowSession && !string.IsNullOrEmpty(context.SessionName))
        {
            parts.Add($"Session: {context.SessionName}");
        }

        if (context.Preferences.ShowContext)
        {
            parts.Add($"Context: {context.ContextWindow.GetDisplayText()}");
            parts.Add(GetContextStatusSymbol(context.ContextWindow));
        }

        if (parts.Count > 0)
        {
            if (_useColors)
            {
                _console.MarkupLine($"[dim]{Markup.Escape(string.Join("  |  ", parts))}[/]");
            }
            else
            {
                _console.WriteLine(string.Join("  |  ", parts));
            }
        }
    }

    private void RenderInfoSection(WelcomeHeaderContext context)
    {
        var prefs = context.Preferences;

        if (prefs.ShowTip)
        {
            RenderTipLine();
        }

        if (prefs.ShowSession || prefs.ShowContext)
        {
            RenderSessionLine(context);
        }
    }

    private static string GetContextStatusSymbol(ContextWindowInfo info)
    {
        if (!info.HasTokenInfo)
            return "🟢";

        return info.UsagePercent switch
        {
            >= 90 => "🔴",
            >= 70 => "🟡",
            _ => "🟢"
        };
    }
}
