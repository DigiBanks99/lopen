using Lopen.Tui.Gallery;
using Spectre.Console;

namespace Lopen.Tui.Tests.Gallery;

public class GalleryComponentRenderTests
{
    [Fact]
    public void WorkflowOverviewComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        WorkflowOverviewGalleryComponent component = new();

        Assert.Equal("Workflow Overview (4 states)", component.DisplayName);
        component.Render(console); // Should not throw
    }

    [Fact]
    public void PromptInputComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        PromptInputGalleryComponent component = new();

        Assert.Equal("Prompt Input", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void CommandPaletteComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        CommandPaletteGalleryComponent component = new();

        Assert.Equal("Command Palette", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void ResponseRenderingComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        ResponseRenderingGalleryComponent component = new();

        Assert.Equal("Response Rendering", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void SessionListComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        SessionListGalleryComponent component = new();

        Assert.Equal("Session List", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void ErrorPanelComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        ErrorPanelGalleryComponent component = new();

        Assert.Equal("Error Panel", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void HelpOutputComponent_RendersWithoutThrowing()
    {
        IAnsiConsole console = CreateConsole();
        HelpOutputGalleryComponent component = new();

        Assert.Equal("Slash Command Help", component.DisplayName);
        component.Render(console);
    }

    [Fact]
    public void AllComponents_HaveUniqueDisplayNames()
    {
        IGalleryComponent[] components = new IGalleryComponent[]
        {
            new WorkflowOverviewGalleryComponent(),
            new PromptInputGalleryComponent(),
            new CommandPaletteGalleryComponent(),
            new ResponseRenderingGalleryComponent(),
            new SessionListGalleryComponent(),
            new ErrorPanelGalleryComponent(),
            new HelpOutputGalleryComponent(),
        };

        HashSet<string> names = new(components.Select(c => c.DisplayName));
        Assert.Equal(components.Length, names.Count);
    }

    [Fact]
    public void AllComponents_MinimumSevenRegistered()
    {
        IGalleryComponent[] components = new IGalleryComponent[]
        {
            new WorkflowOverviewGalleryComponent(),
            new PromptInputGalleryComponent(),
            new CommandPaletteGalleryComponent(),
            new ResponseRenderingGalleryComponent(),
            new SessionListGalleryComponent(),
            new ErrorPanelGalleryComponent(),
            new HelpOutputGalleryComponent(),
        };

        Assert.True(components.Length >= 7, $"Expected at least 7 components but got {components.Length}");
    }

    [Fact]
    public void AllComponents_ImplementIGalleryComponent()
    {
        Type interfaceType = typeof(IGalleryComponent);
        Type[] componentTypes = new[]
        {
            typeof(WorkflowOverviewGalleryComponent),
            typeof(PromptInputGalleryComponent),
            typeof(CommandPaletteGalleryComponent),
            typeof(ResponseRenderingGalleryComponent),
            typeof(SessionListGalleryComponent),
            typeof(ErrorPanelGalleryComponent),
            typeof(HelpOutputGalleryComponent),
        };

        foreach (Type type in componentTypes)
        {
            Assert.True(interfaceType.IsAssignableFrom(type), $"{type.Name} should implement IGalleryComponent");
        }
    }

    private static IAnsiConsole CreateConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });
}
