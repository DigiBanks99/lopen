using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class WelcomeHeaderContextTests
{
    [Fact]
    public void DefaultContext_HasEmptyValues()
    {
        var context = new WelcomeHeaderContext();

        context.Version.ShouldBe("");
        context.SessionName.ShouldBe("");
        context.Terminal.ShouldBeNull();
    }

    [Fact]
    public void Context_CanBeInitialized()
    {
        var context = new WelcomeHeaderContext
        {
            Version = "1.0.0-alpha",
            SessionName = "test-session",
            ContextWindow = new ContextWindowInfo { TokensUsed = 1000, TokensTotal = 100000 }
        };

        context.Version.ShouldBe("1.0.0-alpha");
        context.SessionName.ShouldBe("test-session");
        context.ContextWindow.HasTokenInfo.ShouldBeTrue();
    }
}

public class ContextWindowInfoTests
{
    [Fact]
    public void HasTokenInfo_TrueWhenBothValuesPresent()
    {
        var info = new ContextWindowInfo { TokensUsed = 500, TokensTotal = 1000 };

        info.HasTokenInfo.ShouldBeTrue();
    }

    [Fact]
    public void HasTokenInfo_FalseWhenTokensUsedNull()
    {
        var info = new ContextWindowInfo { TokensTotal = 1000 };

        info.HasTokenInfo.ShouldBeFalse();
    }

    [Fact]
    public void HasTokenInfo_FalseWhenTokensTotalNull()
    {
        var info = new ContextWindowInfo { TokensUsed = 500 };

        info.HasTokenInfo.ShouldBeFalse();
    }

    [Fact]
    public void UsagePercent_CalculatesCorrectly()
    {
        var info = new ContextWindowInfo { TokensUsed = 2500, TokensTotal = 10000 };

        info.UsagePercent.ShouldBe(25.0);
    }

    [Fact]
    public void UsagePercent_ZeroWhenNoTokenInfo()
    {
        var info = new ContextWindowInfo { MessageCount = 5 };

        info.UsagePercent.ShouldBe(0);
    }

    [Fact]
    public void GetDisplayText_FormatsTokensInK()
    {
        var info = new ContextWindowInfo { TokensUsed = 2400, TokensTotal = 128000 };

        info.GetDisplayText().ShouldBe("2.4K/128.0K tokens");
    }

    [Fact]
    public void GetDisplayText_FormatsTokensInM()
    {
        var info = new ContextWindowInfo { TokensUsed = 1500000, TokensTotal = 2000000 };

        info.GetDisplayText().ShouldBe("1.5M/2.0M tokens");
    }

    [Fact]
    public void GetDisplayText_ShowsMessageCount_WhenNoTokenInfo()
    {
        var info = new ContextWindowInfo { MessageCount = 5 };

        info.GetDisplayText().ShouldBe("5 messages");
    }

    [Fact]
    public void GetDisplayText_ShowsSingularMessage()
    {
        var info = new ContextWindowInfo { MessageCount = 1 };

        info.GetDisplayText().ShouldBe("1 message");
    }

    [Fact]
    public void GetDisplayText_FormatsSmallTokenCounts()
    {
        var info = new ContextWindowInfo { TokensUsed = 500, TokensTotal = 999 };

        info.GetDisplayText().ShouldBe("500/999 tokens");
    }
}

public class WelcomeHeaderPreferencesTests
{
    [Fact]
    public void DefaultPreferences_AllEnabled()
    {
        var prefs = new WelcomeHeaderPreferences();

        prefs.ShowLogo.ShouldBeTrue();
        prefs.ShowTip.ShouldBeTrue();
        prefs.ShowContext.ShouldBeTrue();
        prefs.ShowSession.ShouldBeTrue();
    }

    [Fact]
    public void Preferences_CanBeDisabled()
    {
        var prefs = new WelcomeHeaderPreferences
        {
            ShowLogo = false,
            ShowTip = false,
            ShowContext = false,
            ShowSession = false
        };

        prefs.ShowLogo.ShouldBeFalse();
        prefs.ShowTip.ShouldBeFalse();
        prefs.ShowContext.ShouldBeFalse();
        prefs.ShowSession.ShouldBeFalse();
    }
}
