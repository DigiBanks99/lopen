namespace Lopen.Tui.Tests;

/// <summary>
/// Dedicated unit tests for SlashCommandRegistry.
/// Covers JOB-012 — Register(), GetAll(), CreateDefault(), TryParse() edge cases.
/// Requirement: TUI-38 slash command framework.
/// </summary>
public class SlashCommandRegistryTests
{
    // ==================== Register ====================

    [Fact]
    public void Register_OverwritesExistingCommand()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/test", "Original description");
        registry.Register("/test", "Updated description");

        var result = registry.TryParse("/test");

        Assert.NotNull(result);
        Assert.Equal("Updated description", result.Description);
        Assert.Single(registry.GetAll());
    }

    [Fact]
    public void Register_WithAlias_BothKeysResolve()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/help", "Show help", "/h");

        var byCommand = registry.TryParse("/help");
        var byAlias = registry.TryParse("/h");

        Assert.NotNull(byCommand);
        Assert.NotNull(byAlias);
        Assert.Same(byCommand, byAlias);
    }

    [Fact]
    public void Register_WithAlias_GetAllReturnsOnlyOnce()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/help", "Show help", "/h");

        var all = registry.GetAll();

        Assert.Single(all);
        Assert.Equal("/help", all[0].Command);
    }

    [Fact]
    public void Register_MultipleCommands_AllAccessible()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/alpha", "Alpha command");
        registry.Register("/beta", "Beta command");
        registry.Register("/gamma", "Gamma command");

        Assert.NotNull(registry.TryParse("/alpha"));
        Assert.NotNull(registry.TryParse("/beta"));
        Assert.NotNull(registry.TryParse("/gamma"));
        Assert.Equal(3, registry.GetAll().Count);
    }

    [Fact]
    public void Register_OverwritePreservesAlias()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/test", "V1", "/t");
        registry.Register("/test", "V2", "/t");

        var byAlias = registry.TryParse("/t");
        Assert.NotNull(byAlias);
        Assert.Equal("V2", byAlias.Description);
    }

    [Fact]
    public void Register_NewCommandWithSameAlias_OverwritesAliasMapping()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/cmd1", "First", "/x");
        registry.Register("/cmd2", "Second", "/x");

        var byAlias = registry.TryParse("/x");
        Assert.NotNull(byAlias);
        Assert.Equal("/cmd2", byAlias.Command);
    }

    // ==================== GetAll ====================

    [Fact]
    public void GetAll_EmptyRegistry_ReturnsEmptyList()
    {
        var registry = new SlashCommandRegistry();

        var all = registry.GetAll();

        Assert.Empty(all);
    }

    [Fact]
    public void GetAll_ReturnsCommandsInAlphabeticalOrder()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/zebra", "Z command");
        registry.Register("/alpha", "A command");
        registry.Register("/middle", "M command");

        var all = registry.GetAll();

        Assert.Equal("/alpha", all[0].Command);
        Assert.Equal("/middle", all[1].Command);
        Assert.Equal("/zebra", all[2].Command);
    }

    [Fact]
    public void GetAll_ExcludesAliasesFromDistinctList()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/help", "Help", "/h");
        registry.Register("/build", "Build", "/b");

        var all = registry.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, d => d.Command == "/help");
        Assert.Contains(all, d => d.Command == "/build");
    }

    [Fact]
    public void GetAll_ReturnsReadOnlyList()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var all = registry.GetAll();

        Assert.IsAssignableFrom<IReadOnlyList<SlashCommandDefinition>>(all);
    }

    // ==================== CreateDefault ====================

    [Fact]
    public void CreateDefault_Returns8Commands()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Equal(8, registry.GetAll().Count);
    }

    [Fact]
    public void CreateDefault_ContainsAllExpectedCommands()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        var all = registry.GetAll();
        var names = all.Select(d => d.Command).ToList();

        Assert.Contains("/help", names);
        Assert.Contains("/spec", names);
        Assert.Contains("/plan", names);
        Assert.Contains("/build", names);
        Assert.Contains("/session", names);
        Assert.Contains("/config", names);
        Assert.Contains("/revert", names);
        Assert.Contains("/auth", names);
    }

    [Fact]
    public void CreateDefault_AllCommandsHaveDescriptions()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        var all = registry.GetAll();

        Assert.All(all, cmd => Assert.False(
            string.IsNullOrWhiteSpace(cmd.Description),
            $"Command {cmd.Command} has no description"));
    }

    [Fact]
    public void CreateDefault_NoDefaultCommandsHaveAliases()
    {
        var registry = SlashCommandRegistry.CreateDefault();
        var all = registry.GetAll();

        Assert.All(all, cmd => Assert.Null(cmd.Alias));
    }

    [Theory]
    [InlineData("/help", "Show available commands")]
    [InlineData("/spec", "Start requirement gathering")]
    [InlineData("/plan", "Start planning mode")]
    [InlineData("/build", "Start build mode")]
    [InlineData("/session", "Manage sessions")]
    [InlineData("/config", "Show configuration")]
    [InlineData("/revert", "Revert to last checkpoint")]
    [InlineData("/auth", "Authentication commands")]
    public void CreateDefault_CommandHasCorrectDescription(string command, string expectedDescription)
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var def = registry.TryParse(command);

        Assert.NotNull(def);
        Assert.Equal(expectedDescription, def.Description);
    }

    [Fact]
    public void CreateDefault_ReturnsNewInstanceEachCall()
    {
        var registry1 = SlashCommandRegistry.CreateDefault();
        var registry2 = SlashCommandRegistry.CreateDefault();

        Assert.NotSame(registry1, registry2);
    }

    // ==================== TryParse ====================

    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("   "));
        Assert.Null(registry.TryParse("\t"));
        Assert.Null(registry.TryParse("\n"));
    }

    [Fact]
    public void TryParse_SlashOnly_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("/"));
    }

    [Fact]
    public void TryParse_LeadingWhitespace_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        // Leading whitespace means input doesn't StartsWith('/')
        Assert.Null(registry.TryParse(" /help"));
    }

    [Fact]
    public void TryParse_WithArguments_ReturnsCorrectDefinition()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var result = registry.TryParse("/session list");

        Assert.NotNull(result);
        Assert.Equal("/session", result.Command);
    }

    [Fact]
    public void TryParse_CaseInsensitive()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.NotNull(registry.TryParse("/HELP"));
        Assert.NotNull(registry.TryParse("/Help"));
        Assert.NotNull(registry.TryParse("/hElP"));
    }

    [Fact]
    public void TryParse_UnregisteredCommand_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("/unknown"));
        Assert.Null(registry.TryParse("/exit"));
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse(""));
    }

    [Fact]
    public void TryParse_NonSlashInput_ReturnsNull()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        Assert.Null(registry.TryParse("help"));
        Assert.Null(registry.TryParse("hello world"));
    }

    [Fact]
    public void TryParse_AliasReturnsDefinitionWithPrimaryCommand()
    {
        var registry = new SlashCommandRegistry();
        registry.Register("/help", "Show help", "/h");

        var result = registry.TryParse("/h");

        Assert.NotNull(result);
        Assert.Equal("/help", result.Command);
        Assert.Equal("/h", result.Alias);
    }

    [Fact]
    public void TryParse_WithMultipleSpacesInArguments()
    {
        var registry = SlashCommandRegistry.CreateDefault();

        var result = registry.TryParse("/session show --format json");

        Assert.NotNull(result);
        Assert.Equal("/session", result.Command);
    }

    // ==================== SlashCommandDefinition ====================

    [Fact]
    public void SlashCommandDefinition_RecordEquality()
    {
        var def1 = new SlashCommandDefinition("/test", "Test command");
        var def2 = new SlashCommandDefinition("/test", "Test command");

        Assert.Equal(def1, def2);
    }

    [Fact]
    public void SlashCommandDefinition_AliasDefaultsToNull()
    {
        var def = new SlashCommandDefinition("/test", "Test command");

        Assert.Null(def.Alias);
    }

    [Fact]
    public void SlashCommandDefinition_WithAlias()
    {
        var def = new SlashCommandDefinition("/help", "Show help", "/h");

        Assert.Equal("/h", def.Alias);
    }
}
