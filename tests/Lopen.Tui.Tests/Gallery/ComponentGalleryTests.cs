using Lopen.Tui.Gallery;
using NSubstitute;
using Spectre.Console;

namespace Lopen.Tui.Tests.Gallery;

public class ComponentGalleryTests
{
    [Fact]
    public void Constructor_ThrowsOnNullConsole()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ComponentGallery(null!, Array.Empty<IGalleryComponent>()));
    }

    [Fact]
    public void Constructor_ThrowsOnNullComponents()
    {
        IAnsiConsole console = CreateConsole();
        Assert.Throws<ArgumentNullException>(() =>
            new ComponentGallery(console, null!));
    }

    [Fact]
    public void ComponentNames_ReturnsAllRegisteredNames()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent comp1 = CreateMockComponent("Component A");
        IGalleryComponent comp2 = CreateMockComponent("Component B");
        IGalleryComponent comp3 = CreateMockComponent("Component C");

        ComponentGallery gallery = new(console, new[] { comp1, comp2, comp3 });

        Assert.Equal(3, gallery.ComponentNames.Count);
        Assert.Contains("Component A", gallery.ComponentNames);
        Assert.Contains("Component B", gallery.ComponentNames);
        Assert.Contains("Component C", gallery.ComponentNames);
    }

    [Fact]
    public void ComponentNames_EmptyWhenNoComponents()
    {
        IAnsiConsole console = CreateConsole();
        ComponentGallery gallery = new(console, Array.Empty<IGalleryComponent>());

        Assert.Empty(gallery.ComponentNames);
    }

    [Fact]
    public void Run_WithNoComponents_DoesNotThrow()
    {
        IAnsiConsole console = CreateConsole();
        ComponentGallery gallery = new(console, Array.Empty<IGalleryComponent>());

        // Should complete without throwing
        gallery.Run();
    }

    [Fact]
    public void Run_ExitsWhenSelectionReturnsNull()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent comp = CreateMockComponent("Test Component");

        TestableGallery gallery = new(console, new[] { comp }, selectionSequence: new string?[] { null });
        gallery.Run();

        // Component should not have been rendered since selection returned null immediately
        comp.DidNotReceive().Render(Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Run_RendersSelectedComponent()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent comp = CreateMockComponent("Test Component");

        // First call returns the component name, second returns null (exit)
        TestableGallery gallery = new(console, new[] { comp },
            selectionSequence: new string?[] { "Test Component", null });
        gallery.Run();

        comp.Received(1).Render(Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Run_SupportsRepeatedSelections()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent comp = CreateMockComponent("Test Component");

        TestableGallery gallery = new(console, new[] { comp },
            selectionSequence: new string?[] { "Test Component", "Test Component", "Test Component", null });
        gallery.Run();

        comp.Received(3).Render(Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Run_RendersCorrectComponentFromMultiple()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent compA = CreateMockComponent("Alpha");
        IGalleryComponent compB = CreateMockComponent("Beta");

        TestableGallery gallery = new(console, new[] { compA, compB },
            selectionSequence: new string?[] { "Beta", null });
        gallery.Run();

        compA.DidNotReceive().Render(Arg.Any<IAnsiConsole>());
        compB.Received(1).Render(Arg.Any<IAnsiConsole>());
    }

    [Fact]
    public void Run_IgnoresUnknownSelection()
    {
        IAnsiConsole console = CreateConsole();
        IGalleryComponent comp = CreateMockComponent("Real Component");

        // "Ghost" doesn't match any component — should be skipped, then exit
        TestableGallery gallery = new(console, new[] { comp },
            selectionSequence: new string?[] { "Ghost", null });
        gallery.Run();

        comp.DidNotReceive().Render(Arg.Any<IAnsiConsole>());
    }

    private static IGalleryComponent CreateMockComponent(string name)
    {
        IGalleryComponent comp = Substitute.For<IGalleryComponent>();
        comp.DisplayName.Returns(name);
        return comp;
    }

    private static IAnsiConsole CreateConsole() =>
        AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Yes,
            Interactive = InteractionSupport.Yes,
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });

    /// <summary>
    /// Test subclass that overrides interactive methods for deterministic testing.
    /// </summary>
    private sealed class TestableGallery : ComponentGallery
    {
        private readonly Queue<string?> _selections;

        public TestableGallery(IAnsiConsole console, IEnumerable<IGalleryComponent> components, IEnumerable<string?> selectionSequence)
            : base(console, components)
        {
            _selections = new Queue<string?>(selectionSequence);
        }

        internal override string? ShowSelectionList()
        {
            return _selections.Count > 0 ? _selections.Dequeue() : null;
        }

        internal override void WaitForReturn()
        {
            // No-op in tests — don't wait for key press
        }
    }
}
