using Lopen.Core.Documents;

namespace Lopen.Core.Tests.Documents;

public class MarkdownUpdaterTests
{
    [Fact]
    public void ToggleCheckbox_MarksComplete()
    {
        var content = "- [ ] Implement JWT validation\n- [ ] Write tests";

        var result = MarkdownUpdater.ToggleCheckbox(content, "Implement JWT validation", completed: true);

        Assert.Contains("- [x] Implement JWT validation", result);
        Assert.Contains("- [ ] Write tests", result);
    }

    [Fact]
    public void ToggleCheckbox_MarksIncomplete()
    {
        var content = "- [x] Implement JWT validation\n- [x] Write tests";

        var result = MarkdownUpdater.ToggleCheckbox(content, "Implement JWT validation", completed: false);

        Assert.Contains("- [ ] Implement JWT validation", result);
        Assert.Contains("- [x] Write tests", result);
    }

    [Fact]
    public void ToggleCheckbox_CaseInsensitive()
    {
        var content = "- [ ] implement jwt validation";

        var result = MarkdownUpdater.ToggleCheckbox(content, "Implement JWT Validation", completed: true);

        Assert.Contains("[x]", result);
    }

    [Fact]
    public void ToggleCheckbox_NoMatch_ReturnsOriginal()
    {
        var content = "- [ ] Implement JWT validation";

        var result = MarkdownUpdater.ToggleCheckbox(content, "nonexistent task", completed: true);

        Assert.Equal(content, result);
    }

    [Fact]
    public void ToggleCheckbox_IndentedItem()
    {
        var content = "  - [ ] Parse token from header";

        var result = MarkdownUpdater.ToggleCheckbox(content, "Parse token from header", completed: true);

        Assert.Contains("[x]", result);
    }

    [Fact]
    public void CountCheckboxes_AllIncomplete()
    {
        var content = "- [ ] Task 1\n- [ ] Task 2\n- [ ] Task 3";

        var (total, completed) = MarkdownUpdater.CountCheckboxes(content);

        Assert.Equal(3, total);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void CountCheckboxes_Mixed()
    {
        var content = "- [x] Task 1\n- [ ] Task 2\n- [x] Task 3";

        var (total, completed) = MarkdownUpdater.CountCheckboxes(content);

        Assert.Equal(3, total);
        Assert.Equal(2, completed);
    }

    [Fact]
    public void CountCheckboxes_Empty()
    {
        var (total, completed) = MarkdownUpdater.CountCheckboxes("No checkboxes here");

        Assert.Equal(0, total);
        Assert.Equal(0, completed);
    }

    [Fact]
    public void UpdateStatus_ReplacesValue()
    {
        var content = "**Status**: In Progress\nOther content";

        var result = MarkdownUpdater.UpdateStatus(content, "Status", "Complete");

        Assert.Contains("**Status**: Complete", result);
    }

    [Fact]
    public void UpdateStatus_NoMatch_ReturnsOriginal()
    {
        var content = "No status label here";

        var result = MarkdownUpdater.UpdateStatus(content, "Status", "Complete");

        Assert.Equal(content, result);
    }

    [Fact]
    public void ToggleCheckbox_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarkdownUpdater.ToggleCheckbox(null!, "task", true));
    }

    [Fact]
    public void ToggleCheckbox_NullTaskText_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarkdownUpdater.ToggleCheckbox("content", null!, true));
    }

    [Fact]
    public void CountCheckboxes_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarkdownUpdater.CountCheckboxes(null!));
    }

    [Fact]
    public void UpdateStatus_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => MarkdownUpdater.UpdateStatus(null!, "label", "value"));
    }
}
