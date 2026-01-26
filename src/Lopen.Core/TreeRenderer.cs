using Spectre.Console;

namespace Lopen.Core;

/// <summary>
/// Represents a node in a tree structure for hierarchical display.
/// </summary>
public class TreeNode
{
    /// <summary>
    /// The text label for this node.
    /// </summary>
    public string Label { get; }
    
    /// <summary>
    /// Optional icon/emoji prefix for the node.
    /// </summary>
    public string? Icon { get; init; }
    
    /// <summary>
    /// Child nodes.
    /// </summary>
    public IReadOnlyList<TreeNode> Children { get; }
    
    /// <summary>
    /// Maximum characters before truncation (default 80).
    /// </summary>
    public int MaxLabelLength { get; init; } = 80;

    /// <summary>
    /// Creates a new tree node with the specified label.
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <param name="children">Optional child nodes.</param>
    public TreeNode(string label, params TreeNode[] children)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Children = children ?? Array.Empty<TreeNode>();
    }

    /// <summary>
    /// Creates a new tree node with the specified label and child list.
    /// </summary>
    /// <param name="label">The node label.</param>
    /// <param name="children">Child nodes.</param>
    public TreeNode(string label, IEnumerable<TreeNode> children)
    {
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Children = children?.ToList() ?? new List<TreeNode>();
    }

    /// <summary>
    /// Gets the display label (with icon if present, truncated if needed).
    /// </summary>
    public string GetDisplayLabel()
    {
        var label = Label;
        if (label.Length > MaxLabelLength)
        {
            label = label.Substring(0, MaxLabelLength - 3) + "...";
        }
        
        return string.IsNullOrEmpty(Icon) ? label : $"{Icon} {label}";
    }

    /// <summary>
    /// Whether this node has children.
    /// </summary>
    public bool HasChildren => Children.Count > 0;
}

/// <summary>
/// Renders hierarchical tree structures.
/// </summary>
public interface ITreeRenderer
{
    /// <summary>
    /// Renders a tree with the given root node.
    /// </summary>
    /// <param name="root">The root node of the tree.</param>
    /// <param name="title">Optional title for the tree.</param>
    void RenderTree(TreeNode root, string? title = null);
}

/// <summary>
/// Spectre.Console implementation of tree rendering.
/// </summary>
public class SpectreTreeRenderer : ITreeRenderer
{
    private readonly IAnsiConsole _console;
    private const int MaxDepth = 5;

    /// <summary>
    /// Creates a new tree renderer.
    /// </summary>
    /// <param name="console">The console to render to.</param>
    public SpectreTreeRenderer(IAnsiConsole console)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <inheritdoc />
    public void RenderTree(TreeNode root, string? title = null)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));

        var tree = new Tree(Markup.Escape(root.GetDisplayLabel()));
        
        if (!string.IsNullOrEmpty(title))
        {
            tree = new Tree($"[bold]{Markup.Escape(title)}[/]");
            AddNode(tree, root, 0);
        }
        else
        {
            AddChildren(tree, root.Children, 0);
        }

        _console.Write(tree);
    }

    private void AddNode(Tree tree, TreeNode node, int depth)
    {
        var treeNode = tree.AddNode(Markup.Escape(node.GetDisplayLabel()));
        if (depth < MaxDepth)
        {
            foreach (var child in node.Children)
            {
                AddChildNode(treeNode, child, depth + 1);
            }
        }
    }

    private void AddChildren(Tree tree, IReadOnlyList<TreeNode> children, int depth)
    {
        if (depth >= MaxDepth) return;
        
        foreach (var child in children)
        {
            var treeNode = tree.AddNode(Markup.Escape(child.GetDisplayLabel()));
            foreach (var grandchild in child.Children)
            {
                AddChildNode(treeNode, grandchild, depth + 1);
            }
        }
    }

    private void AddChildNode(Spectre.Console.TreeNode parent, TreeNode node, int depth)
    {
        var childNode = parent.AddNode(Markup.Escape(node.GetDisplayLabel()));
        if (depth < MaxDepth)
        {
            foreach (var child in node.Children)
            {
                AddChildNode(childNode, child, depth + 1);
            }
        }
    }
}

/// <summary>
/// Mock tree renderer for testing.
/// </summary>
public class MockTreeRenderer : ITreeRenderer
{
    /// <summary>
    /// All trees that have been rendered.
    /// </summary>
    public List<(TreeNode Root, string? Title)> RenderedTrees { get; } = new();

    /// <inheritdoc />
    public void RenderTree(TreeNode root, string? title = null)
    {
        RenderedTrees.Add((root, title));
    }

    /// <summary>
    /// Gets the last rendered tree root.
    /// </summary>
    public TreeNode? LastRoot => RenderedTrees.Count > 0 ? RenderedTrees[^1].Root : null;

    /// <summary>
    /// Gets the last rendered tree title.
    /// </summary>
    public string? LastTitle => RenderedTrees.Count > 0 ? RenderedTrees[^1].Title : null;

    /// <summary>
    /// Resets the renderer state.
    /// </summary>
    public void Reset() => RenderedTrees.Clear();
}
