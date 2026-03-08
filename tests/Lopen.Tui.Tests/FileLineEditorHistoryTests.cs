namespace Lopen.Tui.Tests;

public class FileLineEditorHistoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _historyPath;

    public FileLineEditorHistoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _historyPath = Path.Combine(_tempDir, "history.txt");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void NewHistory_HasZeroCount()
    {
        FileLineEditorHistory history = new(_historyPath);
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void Add_IncrementsCount()
    {
        FileLineEditorHistory history = new(_historyPath);
        history.Add("test command");
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void Add_EmptyString_IsIgnored()
    {
        FileLineEditorHistory history = new(_historyPath);
        history.Add("");
        history.Add("   ");
        Assert.Equal(0, history.Count);
    }

    [Fact]
    public void Add_ConsecutiveDuplicate_IsIgnored()
    {
        FileLineEditorHistory history = new(_historyPath);
        history.Add("same");
        history.Add("same");
        history.Add("same");
        Assert.Equal(1, history.Count);
    }

    [Fact]
    public void Add_NonConsecutiveDuplicate_IsKept()
    {
        FileLineEditorHistory history = new(_historyPath);
        history.Add("first");
        history.Add("second");
        history.Add("first");
        Assert.Equal(3, history.Count);
    }

    [Fact]
    public void History_PersistsAcrossInstances()
    {
        FileLineEditorHistory history1 = new(_historyPath);
        history1.Add("command one");
        history1.Add("command two");

        FileLineEditorHistory history2 = new(_historyPath);
        Assert.Equal(2, history2.Count);
    }

    [Fact]
    public void History_CreatesMissingDirectory()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "history.txt");
        FileLineEditorHistory history = new(nestedPath);
        history.Add("test");
        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void History_LoadsAndDeduplicatesFromFile()
    {
        // Write a file with consecutive duplicates
        File.WriteAllLines(_historyPath, ["a", "a", "b", "b", "a"]);

        FileLineEditorHistory history = new(_historyPath);
        Assert.Equal(3, history.Count); // a, b, a (deduped consecutives)
    }

    [Fact]
    public void Constructor_ThrowsOnNullPath()
    {
        Assert.Throws<ArgumentNullException>(() => new FileLineEditorHistory(null!));
    }
}
