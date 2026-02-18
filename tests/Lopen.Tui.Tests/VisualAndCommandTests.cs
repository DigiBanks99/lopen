using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for SpinnerComponent, ColorPalette, UnicodeSupport, and SlashCommandRegistry.
/// Covers JOB-094, JOB-095, JOB-096 acceptance criteria.
/// </summary>
public class VisualAndCommandTests
{
    // ==================== SpinnerComponent ====================

    private readonly SpinnerComponent _spinner = new();

    [Fact]
    public void Spinner_Name_IsCorrect()
    {
        Assert.Equal("Spinner", _spinner.Name);
    }

    [Fact]
    public void Spinner_ShowsMessageAndFrame()
    {
        var data = new SpinnerData { Message = "Loading..." };
        var result = _spinner.Render(data, 40);

        Assert.Contains("Loading...", result);
        Assert.StartsWith("⠋", result);
    }

    [Fact]
    public void Spinner_WithProgress_ShowsPercentage()
    {
        var data = new SpinnerData { Message = "Building", ProgressPercent = 75 };
        var result = _spinner.Render(data, 40);

        Assert.Contains("75%", result);
    }

    [Fact]
    public void Spinner_IndeterminateProgress_NoPercentage()
    {
        var data = new SpinnerData { Message = "Working" };
        var result = _spinner.Render(data, 40);

        Assert.DoesNotContain("%", result);
    }

    [Fact]
    public void Spinner_FrameAdvances()
    {
        var r1 = _spinner.Render(new SpinnerData { Message = "A", Frame = 0 }, 40);
        var r2 = _spinner.Render(new SpinnerData { Message = "A", Frame = 1 }, 40);

        Assert.NotEqual(r1[..1], r2[..1]);
    }

    [Fact]
    public void Spinner_FrameWraps()
    {
        var r0 = _spinner.Render(new SpinnerData { Message = "A", Frame = 0 }, 40);
        var r10 = _spinner.Render(new SpinnerData { Message = "A", Frame = 10 }, 40);

        Assert.Equal(r0[..1], r10[..1]); // Same frame after wrap
    }

    [Fact]
    public void Spinner_ZeroWidth_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, _spinner.Render(new SpinnerData { Message = "X" }, 0));
    }

    [Fact]
    public void Spinner_PaddedToWidth()
    {
        var result = _spinner.Render(new SpinnerData { Message = "Hi" }, 40);
        Assert.Equal(40, result.Length);
    }

    // ==================== ColorPalette ====================

    [Fact]
    public void ColorPalette_WithColor_ReturnsAnsiCodes()
    {
        var palette = new ColorPalette(noColor: false);

        Assert.StartsWith("\x1b[", palette.Success);
        Assert.StartsWith("\x1b[", palette.Error);
        Assert.Equal("\x1b[0m", palette.Reset);
    }

    [Fact]
    public void ColorPalette_NoColor_ReturnsEmpty()
    {
        var palette = new ColorPalette(noColor: true);

        Assert.Equal("", palette.Success);
        Assert.Equal("", palette.Error);
        Assert.Equal("", palette.Reset);
        Assert.Equal("", palette.Bold);
    }

    // ==================== TUI-32: Semantic Color Palette Verification ====================

    [Fact]
    public void ColorPalette_AllProperties_ReturnAnsiCodes_WhenEnabled()
    {
        var palette = new ColorPalette(noColor: false);

        Assert.Equal("\x1b[32m", palette.Success);  // green
        Assert.Equal("\x1b[31m", palette.Error);     // red
        Assert.Equal("\x1b[33m", palette.Warning);   // yellow
        Assert.Equal("\x1b[36m", palette.Info);       // cyan
        Assert.Equal("\x1b[90m", palette.Muted);     // dim
        Assert.Equal("\x1b[35m", palette.Accent);    // magenta
        Assert.Equal("\x1b[0m", palette.Reset);
        Assert.Equal("\x1b[1m", palette.Bold);
    }

    [Fact]
    public void ColorPalette_AllProperties_ReturnEmpty_WhenDisabled()
    {
        var palette = new ColorPalette(noColor: true);

        Assert.Equal("", palette.Warning);
        Assert.Equal("", palette.Info);
        Assert.Equal("", palette.Muted);
        Assert.Equal("", palette.Accent);
    }

    [Fact]
    public void ColorPalette_AllSemanticColors_AreDistinct()
    {
        var palette = new ColorPalette(noColor: false);
        var codes = new[] { palette.Success, palette.Error, palette.Warning, palette.Info, palette.Muted, palette.Accent };
        Assert.Equal(codes.Length, codes.Distinct().Count());
    }

    // ==================== TUI-37: NO_COLOR Environment Variable ====================

    [Fact]
    public void ColorPalette_NoColorEnvVar_DisablesColors()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        try
        {
            var palette = new ColorPalette();
            Assert.True(palette.NoColor);
            Assert.Equal("", palette.Success);
            Assert.Equal("", palette.Error);
            Assert.Equal("", palette.Reset);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", null);
        }
    }

    [Fact]
    public void ColorPalette_NoColorEnvVarNotSet_EnablesColors()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", null);
        var palette = new ColorPalette();
        Assert.False(palette.NoColor);
        Assert.StartsWith("\x1b[", palette.Success);
    }

    [Fact]
    public void ColorPalette_NoColorEnvVarEmpty_EnablesColors()
    {
        Environment.SetEnvironmentVariable("NO_COLOR", "");
        try
        {
            var palette = new ColorPalette();
            Assert.False(palette.NoColor);
            Assert.StartsWith("\x1b[", palette.Success);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NO_COLOR", null);
        }
    }

    // ==================== UnicodeSupport ====================

    [Fact]
    public void Unicode_DefaultMode_ReturnsUnicode()
    {
        UnicodeSupport.UseAscii = false;

        Assert.Equal("┌", UnicodeSupport.TopLeft);
        Assert.Equal("─", UnicodeSupport.Horizontal);
        Assert.Equal("│", UnicodeSupport.Vertical);
        Assert.Equal("✓", UnicodeSupport.CheckMark);
        Assert.Equal("▶", UnicodeSupport.Arrow);
        Assert.Equal("○", UnicodeSupport.Circle);
    }

    [Fact]
    public void Unicode_AsciiMode_ReturnsFallbacks()
    {
        UnicodeSupport.UseAscii = true;

        Assert.Equal("+", UnicodeSupport.TopLeft);
        Assert.Equal("-", UnicodeSupport.Horizontal);
        Assert.Equal("|", UnicodeSupport.Vertical);
        Assert.Equal("[x]", UnicodeSupport.CheckMark);
        Assert.Equal(">", UnicodeSupport.Arrow);
        Assert.Equal("o", UnicodeSupport.Circle);

        // Reset
        UnicodeSupport.UseAscii = false;
    }

    // ==================== TUI-34: Box-Drawing Characters ====================
    // ==================== TUI-33: Unicode ASCII Fallback Verification ====================

    [Fact]
    public void Unicode_AllBoxDrawing_ReturnUnicode()
    {
        UnicodeSupport.UseAscii = false;
        Assert.Equal("┐", UnicodeSupport.TopRight);
        Assert.Equal("└", UnicodeSupport.BottomLeft);
        Assert.Equal("┘", UnicodeSupport.BottomRight);
        Assert.Equal("├", UnicodeSupport.TeeRight);
        Assert.Equal("┤", UnicodeSupport.TeeLeft);
        Assert.Equal("┼", UnicodeSupport.Cross);
    }

    [Fact]
    public void Unicode_AllStatusIcons_ReturnUnicode()
    {
        UnicodeSupport.UseAscii = false;
        Assert.Equal("✗", UnicodeSupport.Cross_Icon);
        Assert.Equal("●", UnicodeSupport.FilledCircle);
        Assert.Equal("◆", UnicodeSupport.Diamond);
        Assert.Equal("⚠", UnicodeSupport.Warning_Icon);
    }

    [Fact]
    public void Unicode_AllBoxDrawing_ReturnAsciiFallbacks()
    {
        UnicodeSupport.UseAscii = true;
        Assert.Equal("+", UnicodeSupport.TopRight);
        Assert.Equal("+", UnicodeSupport.BottomLeft);
        Assert.Equal("+", UnicodeSupport.BottomRight);
        Assert.Equal("+", UnicodeSupport.TeeRight);
        Assert.Equal("+", UnicodeSupport.TeeLeft);
        Assert.Equal("+", UnicodeSupport.Cross);
        UnicodeSupport.UseAscii = false;
    }

    [Fact]
    public void Unicode_AllStatusIcons_ReturnAsciiFallbacks()
    {
        UnicodeSupport.UseAscii = true;
        Assert.Equal("[!]", UnicodeSupport.Cross_Icon);
        Assert.Equal("*", UnicodeSupport.FilledCircle);
        Assert.Equal("<>", UnicodeSupport.Diamond);
        Assert.Equal("!!", UnicodeSupport.Warning_Icon);
        UnicodeSupport.UseAscii = false;
    }

    [Fact]
    public void Unicode_AsciiFallbacks_ArePureAscii()
    {
        UnicodeSupport.UseAscii = true;
        var all = new[] {
            UnicodeSupport.TopLeft, UnicodeSupport.TopRight,
            UnicodeSupport.BottomLeft, UnicodeSupport.BottomRight,
            UnicodeSupport.Horizontal, UnicodeSupport.Vertical,
            UnicodeSupport.TeeRight, UnicodeSupport.TeeLeft, UnicodeSupport.Cross,
            UnicodeSupport.CheckMark, UnicodeSupport.Cross_Icon,
            UnicodeSupport.Arrow, UnicodeSupport.Circle,
            UnicodeSupport.FilledCircle, UnicodeSupport.Diamond,
            UnicodeSupport.Warning_Icon
        };
        foreach (var s in all)
        {
            Assert.All(s.ToCharArray(), c => Assert.True(c <= 127, $"'{c}' (U+{(int)c:X4}) is not ASCII in '{s}'"));
        }
        UnicodeSupport.UseAscii = false;
    }

    [Fact]
    public void Unicode_UnicodeValues_AreNonAscii()
    {
        UnicodeSupport.UseAscii = false;
        var boxAndIcons = new[] {
            UnicodeSupport.TopLeft, UnicodeSupport.TopRight,
            UnicodeSupport.BottomLeft, UnicodeSupport.BottomRight,
            UnicodeSupport.Horizontal, UnicodeSupport.Vertical,
            UnicodeSupport.TeeRight, UnicodeSupport.TeeLeft, UnicodeSupport.Cross,
            UnicodeSupport.CheckMark, UnicodeSupport.Cross_Icon,
            UnicodeSupport.Arrow, UnicodeSupport.Circle,
            UnicodeSupport.FilledCircle, UnicodeSupport.Diamond,
            UnicodeSupport.Warning_Icon
        };
        foreach (var s in boxAndIcons)
        {
            Assert.Contains(s.ToCharArray(), c => c > 127);
        }
    }

    // ==================== SlashCommandRegistry ====================

    [Fact]
    public void SlashRegistry_Default_Has8Commands()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        var all = registry.GetAll();

        Assert.Equal(8, all.Count);
    }

    [Fact]
    public void SlashRegistry_TryParse_RecognizesCommand()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var cmd = registry.TryParse("/help");
        Assert.NotNull(cmd);
        Assert.Equal("/help", cmd.Command);
        Assert.Contains("available commands", cmd.Description);
    }

    [Fact]
    public void SlashRegistry_TryParse_CaseInsensitive()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.NotNull(registry.TryParse("/HELP"));
        Assert.NotNull(registry.TryParse("/Spec"));
    }

    [Fact]
    public void SlashRegistry_TryParse_UnknownCommand_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("/unknown"));
    }

    [Fact]
    public void SlashRegistry_TryParse_NonSlash_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("help"));
        Assert.Null(registry.TryParse(""));
        Assert.Null(registry.TryParse(null!));
    }

    [Fact]
    public void SlashRegistry_TryParse_WithArguments_StillRecognizes()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var cmd = registry.TryParse("/build --module auth");
        Assert.NotNull(cmd);
        Assert.Equal("/build", cmd.Command);
    }

    [Fact]
    public void SlashRegistry_Register_CustomCommand()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/custom", "Do something custom");

        var cmd = registry.TryParse("/custom");
        Assert.NotNull(cmd);
        Assert.Equal("/custom", cmd.Command);
    }

    [Fact]
    public void SlashRegistry_Alias_Works()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/quit", "Exit Lopen", alias: "/q");

        Assert.NotNull(registry.TryParse("/quit"));
        Assert.NotNull(registry.TryParse("/q"));
        Assert.Equal(registry.TryParse("/quit")!.Command, registry.TryParse("/q")!.Command);
    }

    [Fact]
    public void SlashRegistry_DefaultCommands_AreCorrect()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        var names = registry.GetAll().Select(c => c.Command).ToList();

        Assert.Contains("/help", names);
        Assert.Contains("/spec", names);
        Assert.Contains("/plan", names);
        Assert.Contains("/build", names);
        Assert.Contains("/session", names);
        Assert.Contains("/config", names);
        Assert.Contains("/revert", names);
        Assert.Contains("/auth", names);
    }
}
