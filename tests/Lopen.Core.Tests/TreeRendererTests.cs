using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace Lopen.Core.Tests;

public class TreeNodeTests
{
    [Fact]
    public void Constructor_WithLabel_SetsLabel()
    {
        var node = new TreeNode("Root");

        node.Label.ShouldBe("Root");
        node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WithChildren_SetsChildren()
    {
        var child1 = new TreeNode("Child 1");
        var child2 = new TreeNode("Child 2");
        var node = new TreeNode("Root", child1, child2);

        node.Children.Count.ShouldBe(2);
        node.Children[0].Label.ShouldBe("Child 1");
        node.Children[1].Label.ShouldBe("Child 2");
    }

    [Fact]
    public void Constructor_WithNullLabel_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new TreeNode(null!));
    }

    [Fact]
    public void GetDisplayLabel_WithIcon_CombinesIconAndLabel()
    {
        var node = new TreeNode("File.txt") { Icon = "📄" };

        node.GetDisplayLabel().ShouldBe("📄 File.txt");
    }

    [Fact]
    public void GetDisplayLabel_WithoutIcon_ReturnsLabel()
    {
        var node = new TreeNode("File.txt");

        node.GetDisplayLabel().ShouldBe("File.txt");
    }

    [Fact]
    public void GetDisplayLabel_LongLabel_TruncatesWithEllipsis()
    {
        var longLabel = new string('a', 100);
        var node = new TreeNode(longLabel);

        var result = node.GetDisplayLabel();

        result.Length.ShouldBe(80);
        result.ShouldEndWith("...");
    }

    [Fact]
    public void GetDisplayLabel_ExactlyMaxLength_NoTruncation()
    {
        var exactLabel = new string('a', 80);
        var node = new TreeNode(exactLabel);

        var result = node.GetDisplayLabel();

        result.ShouldBe(exactLabel);
    }

    [Fact]
    public void HasChildren_WithChildren_ReturnsTrue()
    {
        var node = new TreeNode("Parent", new TreeNode("Child"));

        node.HasChildren.ShouldBeTrue();
    }

    [Fact]
    public void HasChildren_WithoutChildren_ReturnsFalse()
    {
        var node = new TreeNode("Leaf");

        node.HasChildren.ShouldBeFalse();
    }
}

public class SpectreTreeRendererTests
{
    [Fact]
    public void RenderTree_SimpleNode_OutputsLabel()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);
        var root = new TreeNode("Root");

        renderer.RenderTree(root);

        console.Output.ShouldContain("Root");
    }

    [Fact]
    public void RenderTree_WithChildren_OutputsHierarchy()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);
        var root = new TreeNode("Root",
            new TreeNode("Child 1"),
            new TreeNode("Child 2")
        );

        renderer.RenderTree(root);

        console.Output.ShouldContain("Root");
        console.Output.ShouldContain("Child 1");
        console.Output.ShouldContain("Child 2");
    }

    [Fact]
    public void RenderTree_WithTitle_IncludesTitle()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);
        var root = new TreeNode("Content");

        renderer.RenderTree(root, "My Tree");

        console.Output.ShouldContain("My Tree");
        console.Output.ShouldContain("Content");
    }

    [Fact]
    public void RenderTree_NestedChildren_OutputsAllLevels()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);
        var root = new TreeNode("Level 0",
            new TreeNode("Level 1",
                new TreeNode("Level 2",
                    new TreeNode("Level 3")
                )
            )
        );

        renderer.RenderTree(root);

        console.Output.ShouldContain("Level 0");
        console.Output.ShouldContain("Level 1");
        console.Output.ShouldContain("Level 2");
        console.Output.ShouldContain("Level 3");
    }

    [Fact]
    public void RenderTree_WithIcons_DisplaysIcons()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);
        var root = new TreeNode("Files") { Icon = "📁" };

        renderer.RenderTree(root);

        console.Output.ShouldContain("📁 Files");
    }

    [Fact]
    public void RenderTree_NullRoot_Throws()
    {
        var console = new TestConsole();
        var renderer = new SpectreTreeRenderer(console);

        Should.Throw<ArgumentNullException>(() => renderer.RenderTree(null!));
    }

    [Fact]
    public void Constructor_NullConsole_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new SpectreTreeRenderer(null!));
    }
}

public class MockTreeRendererTests
{
    [Fact]
    public void RenderTree_TracksRenderedTrees()
    {
        var renderer = new MockTreeRenderer();
        var root = new TreeNode("Root");

        renderer.RenderTree(root, "Title");

        renderer.RenderedTrees.Count.ShouldBe(1);
        renderer.LastRoot.ShouldBe(root);
        renderer.LastTitle.ShouldBe("Title");
    }

    [Fact]
    public void RenderTree_MultipleTimes_TracksAll()
    {
        var renderer = new MockTreeRenderer();
        var root1 = new TreeNode("First");
        var root2 = new TreeNode("Second");

        renderer.RenderTree(root1);
        renderer.RenderTree(root2, "Second Tree");

        renderer.RenderedTrees.Count.ShouldBe(2);
        renderer.LastRoot!.Label.ShouldBe("Second");
    }

    [Fact]
    public void Reset_ClearsTrees()
    {
        var renderer = new MockTreeRenderer();
        renderer.RenderTree(new TreeNode("Test"));

        renderer.Reset();

        renderer.RenderedTrees.ShouldBeEmpty();
        renderer.LastRoot.ShouldBeNull();
    }

    [Fact]
    public void LastRoot_WhenEmpty_ReturnsNull()
    {
        var renderer = new MockTreeRenderer();

        renderer.LastRoot.ShouldBeNull();
        renderer.LastTitle.ShouldBeNull();
    }
}

public class ConsoleOutputTreeTests
{
    [Fact]
    public void Tree_RendersTreeNode()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var root = new TreeNode("Project",
            new TreeNode("src"),
            new TreeNode("tests")
        );

        output.Tree(root);

        console.Output.ShouldContain("Project");
        console.Output.ShouldContain("src");
        console.Output.ShouldContain("tests");
    }

    [Fact]
    public void Tree_WithTitle_RendersWithTitle()
    {
        var console = new TestConsole();
        var output = new ConsoleOutput(console);
        var root = new TreeNode("Content");

        output.Tree(root, "Directory Structure");

        console.Output.ShouldContain("Directory Structure");
    }
}
