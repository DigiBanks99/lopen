using Lopen.Tui;

namespace Lopen.Tui.Tests;

/// <summary>
/// Tests for FilePickerComponent, SelectionModalComponent, ConfirmationModalComponent, ErrorModalComponent.
/// Covers JOB-091 and JOB-093 acceptance criteria.
/// </summary>
public class SelectionComponentTests
{
    // ==================== FilePickerComponent ====================

    private readonly FilePickerComponent _filePicker = new();

    [Fact]
    public void FilePicker_ShowsRootPath()
    {
        var data = new FilePickerData
        {
            RootPath = "/home/project",
            Nodes = [new FileNode("src", true, 0, true), new FileNode("auth.ts", false, 1)],
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("📂 /home/project"));
    }

    [Fact]
    public void FilePicker_ShowsTreeNodes()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("src", true, 0, true), new FileNode("index.ts", false, 1)],
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("📂 src"));
        Assert.Contains(lines, l => l.Contains("📄 index.ts"));
    }

    [Fact]
    public void FilePicker_HighlightsSelected()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("a.ts", false, 0), new FileNode("b.ts", false, 0)],
            SelectedIndex = 1,
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("▶") && l.Contains("b.ts"));
    }

    [Fact]
    public void FilePicker_ClosedDir_ShowsClosedIcon()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("lib", true, 0, false)],
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("📁 lib"));
    }

    [Fact]
    public void FilePicker_ZeroRegion_ReturnsEmpty()
    {
        var data = new FilePickerData { RootPath = "/p" };
        Assert.Empty(_filePicker.Render(data, new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== SelectionModalComponent ====================

    private readonly SelectionModalComponent _selection = new();

    [Fact]
    public void Selection_ShowsTitle()
    {
        var data = new ModuleSelectionData { Title = "Select Module", Options = ["auth", "core"] };
        var lines = _selection.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Select Module"));
    }

    [Fact]
    public void Selection_ShowsOptions()
    {
        var data = new ModuleSelectionData { Title = "Select", Options = ["auth", "core", "storage"] };
        var lines = _selection.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("auth"));
        Assert.Contains(lines, l => l.Contains("core"));
        Assert.Contains(lines, l => l.Contains("storage"));
    }

    [Fact]
    public void Selection_HighlightsSelected()
    {
        var data = new ModuleSelectionData { Title = "Select", Options = ["a", "b", "c"], SelectedIndex = 1 };
        var lines = _selection.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("▶ b"));
    }

    [Fact]
    public void Selection_ZeroRegion_ReturnsEmpty()
    {
        var data = new ModuleSelectionData { Title = "X" };
        Assert.Empty(_selection.Render(data, new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== ConfirmationModalComponent ====================

    private readonly ConfirmationModalComponent _confirm = new();

    [Fact]
    public void Confirmation_ShowsTitle()
    {
        var data = new ConfirmationData { Title = "Apply changes?" };
        var lines = _confirm.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Apply changes?"));
    }

    [Fact]
    public void Confirmation_ShowsMessage()
    {
        var data = new ConfirmationData { Title = "Apply?", Message = "This will modify 3 files" };
        var lines = _confirm.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("This will modify 3 files"));
    }

    [Fact]
    public void Confirmation_ShowsOptions()
    {
        var data = new ConfirmationData { Title = "Confirm", Options = ["Yes", "No", "Always"] };
        var lines = _confirm.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>Yes<]") && l.Contains("[No]") && l.Contains("[Always]"));
    }

    [Fact]
    public void Confirmation_HighlightsSelected()
    {
        var data = new ConfirmationData { Title = "Confirm", Options = ["Yes", "No"], SelectedIndex = 1 };
        var lines = _confirm.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>No<]") && l.Contains("[Yes]"));
    }

    [Fact]
    public void Confirmation_ZeroRegion_ReturnsEmpty()
    {
        Assert.Empty(_confirm.Render(new ConfirmationData { Title = "X" }, new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== ErrorModalComponent ====================

    private readonly ErrorModalComponent _error = new();

    [Fact]
    public void ErrorModal_ShowsTitle()
    {
        var data = new ErrorModalData { Title = "Build Failed", Message = "Compilation error" };
        var lines = _error.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("⚠ Build Failed"));
    }

    [Fact]
    public void ErrorModal_ShowsMessage()
    {
        var data = new ErrorModalData { Title = "Error", Message = "Test failure on line 42" };
        var lines = _error.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("Test failure on line 42"));
    }

    [Fact]
    public void ErrorModal_ShowsRecoveryOptions()
    {
        var data = new ErrorModalData { Title = "Error", Message = "Fail" };
        var lines = _error.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>Retry<]") && l.Contains("[Skip]") && l.Contains("[Abort]"));
    }

    [Fact]
    public void ErrorModal_HighlightsSelected()
    {
        var data = new ErrorModalData { Title = "Error", Message = "Fail", SelectedIndex = 2 };
        var lines = _error.Render(data, new ScreenRect(0, 0, 60, 10));

        Assert.Contains(lines, l => l.Contains("[>Abort<]"));
    }

    [Fact]
    public void ErrorModal_ZeroRegion_ReturnsEmpty()
    {
        Assert.Empty(_error.Render(new ErrorModalData { Title = "X", Message = "Y" }, new ScreenRect(0, 0, 0, 10)));
    }

    // ==================== FilePickerComponent additional (JOB-049 / TUI-16) ====================

    [Fact]
    public void FilePicker_DirectoryCollapsed_ShowsFolderIcon()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("src", true, 0, false)]
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 5));
        Assert.Contains(lines, l => l.Contains("📁 src"));
    }

    [Fact]
    public void FilePicker_DirectoryExpanded_ShowsOpenFolderIcon()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("src", true, 0, true)]
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 5));
        Assert.Contains(lines, l => l.Contains("📂 src"));
    }

    [Fact]
    public void FilePicker_FileNode_ShowsFileIcon()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [new FileNode("readme.md", false, 0)]
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 5));
        Assert.Contains(lines, l => l.Contains("📄 readme.md"));
    }

    [Fact]
    public void FilePicker_Depth_IndentsCorrectly()
    {
        var data = new FilePickerData
        {
            RootPath = "/project",
            Nodes = [
                new FileNode("src", true, 0, true),
                new FileNode("deep.ts", false, 2)
            ]
        };
        var lines = _filePicker.Render(data, new ScreenRect(0, 0, 60, 5));
        // Depth 2 should have more indentation than depth 0
        var srcLine = lines.First(l => l.Contains("src"));
        var deepLine = lines.First(l => l.Contains("deep.ts"));
        var srcIndent = srcLine.IndexOf("📂");
        var deepIndent = deepLine.IndexOf("📄");
        Assert.True(deepIndent > srcIndent);
    }
}
