using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class CommandHistoryTests
{
    [Fact]
    public void Add_StoresCommand()
    {
        var history = new CommandHistory();

        history.Add("version");

        history.Count.ShouldBe(1);
        history.GetAll().ShouldContain("version");
    }

    [Fact]
    public void Add_IgnoresEmptyCommands()
    {
        var history = new CommandHistory();

        history.Add("");
        history.Add("   ");
        history.Add(null!);

        history.Count.ShouldBe(0);
    }

    [Fact]
    public void Add_IgnoresDuplicateLastCommand()
    {
        var history = new CommandHistory();

        history.Add("version");
        history.Add("version");
        history.Add("version");

        history.Count.ShouldBe(1);
    }

    [Fact]
    public void Add_AllowsDuplicatesIfNotLast()
    {
        var history = new CommandHistory();

        history.Add("version");
        history.Add("help");
        history.Add("version");

        history.Count.ShouldBe(3);
    }

    [Fact]
    public void Add_RemovesOldestWhenAtCapacity()
    {
        var history = new CommandHistory(maxSize: 3);

        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");
        history.Add("cmd4");

        history.Count.ShouldBe(3);
        history.GetAll().ShouldNotContain("cmd1");
        history.GetAll().ShouldContain("cmd2");
        history.GetAll().ShouldContain("cmd3");
        history.GetAll().ShouldContain("cmd4");
    }

    [Fact]
    public void GetPrevious_ReturnsLastCommand()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        var result = history.GetPrevious();

        result.ShouldBe("cmd3");
    }

    [Fact]
    public void GetPrevious_NavigatesBackward()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        history.GetPrevious().ShouldBe("cmd3");
        history.GetPrevious().ShouldBe("cmd2");
        history.GetPrevious().ShouldBe("cmd1");
    }

    [Fact]
    public void GetPrevious_StopsAtBeginning()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");

        history.GetPrevious(); // cmd2
        history.GetPrevious(); // cmd1
        history.GetPrevious().ShouldBe("cmd1"); // Still cmd1
        history.GetPrevious().ShouldBe("cmd1"); // Still cmd1
    }

    [Fact]
    public void GetPrevious_ReturnsNullWhenEmpty()
    {
        var history = new CommandHistory();

        history.GetPrevious().ShouldBeNull();
    }

    [Fact]
    public void GetNext_NavigatesForward()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        history.GetPrevious(); // cmd3
        history.GetPrevious(); // cmd2
        history.GetPrevious(); // cmd1

        history.GetNext().ShouldBe("cmd2");
        history.GetNext().ShouldBe("cmd3");
    }

    [Fact]
    public void GetNext_ReturnsNullAtEnd()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");

        history.GetPrevious(); // cmd2
        history.GetPrevious(); // cmd1
        history.GetNext(); // cmd2
        history.GetNext().ShouldBeNull(); // Past end
    }

    [Fact]
    public void GetNext_ReturnsNullWhenNotNavigating()
    {
        var history = new CommandHistory();
        history.Add("cmd1");

        history.GetNext().ShouldBeNull();
    }

    [Fact]
    public void ResetPosition_MovesToEnd()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        history.GetPrevious(); // cmd3
        history.GetPrevious(); // cmd2
        history.ResetPosition();

        history.GetPrevious().ShouldBe("cmd3");
    }

    [Fact]
    public void Add_ResetsPosition()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");

        history.GetPrevious(); // cmd2
        history.GetPrevious(); // cmd1

        history.Add("cmd3");

        history.GetPrevious().ShouldBe("cmd3");
    }

    [Fact]
    public void Clear_RemovesAllHistory()
    {
        var history = new CommandHistory();
        history.Add("cmd1");
        history.Add("cmd2");

        history.Clear();

        history.Count.ShouldBe(0);
        history.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidMaxSize()
    {
        var act = () => new CommandHistory(maxSize: 0);

        Should.Throw<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void MaxSize_ReturnsConfiguredValue()
    {
        var history = new CommandHistory(maxSize: 50);

        history.MaxSize.ShouldBe(50);
    }

    [Fact]
    public void DefaultMaxSize_Is1000()
    {
        CommandHistory.DefaultMaxSize.ShouldBe(1000);
    }

    [Fact]
    public void GetAll_ReturnsReadOnlyCopy()
    {
        var history = new CommandHistory();
        history.Add("cmd1");

        var all = history.GetAll();

        all.ShouldBeAssignableTo<IReadOnlyList<string>>();
    }
}

public class PersistentCommandHistoryTests : IDisposable
{
    private readonly string _tempFile;

    public PersistentCommandHistoryTests()
    {
        _tempFile = Path.GetTempFileName();
        // Clean up the file created by GetTempFileName
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void Add_PersistsToFile()
    {
        var history = new PersistentCommandHistory(_tempFile);

        history.Add("version");
        history.Add("help");

        File.Exists(_tempFile).ShouldBeTrue();
        var lines = File.ReadAllLines(_tempFile);
        lines.Count().ShouldBe(2);
        lines.ShouldContain("version");
        lines.ShouldContain("help");
    }

    [Fact]
    public void Constructor_LoadsExistingHistory()
    {
        File.WriteAllLines(_tempFile, ["cmd1", "cmd2", "cmd3"]);

        var history = new PersistentCommandHistory(_tempFile);

        history.Count.ShouldBe(3);
        history.GetAll().ShouldContain("cmd1");
        history.GetAll().ShouldContain("cmd2");
        history.GetAll().ShouldContain("cmd3");
    }

    [Fact]
    public void Constructor_HandlesNonexistentFile()
    {
        var history = new PersistentCommandHistory(_tempFile);

        history.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_RemovesHistoryFromFile()
    {
        var history = new PersistentCommandHistory(_tempFile);
        history.Add("cmd1");
        history.Add("cmd2");

        history.Clear();

        File.ReadAllText(_tempFile).Trim().ShouldBeEmpty();
    }

    [Fact]
    public void GetPrevious_WorksWithLoadedHistory()
    {
        File.WriteAllLines(_tempFile, ["cmd1", "cmd2"]);

        var history = new PersistentCommandHistory(_tempFile);

        history.GetPrevious().ShouldBe("cmd2");
        history.GetPrevious().ShouldBe("cmd1");
    }

    [Fact]
    public void MaxSize_IsRespected()
    {
        var history = new PersistentCommandHistory(_tempFile, maxSize: 2);

        history.Add("cmd1");
        history.Add("cmd2");
        history.Add("cmd3");

        history.Count.ShouldBe(2);
        history.GetAll().ShouldNotContain("cmd1");
    }

    [Fact]
    public void CreatesDirectoryIfNotExists()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var historyPath = Path.Combine(tempDir, "history");

        try
        {
            var history = new PersistentCommandHistory(historyPath);
            history.Add("test");

            Directory.Exists(tempDir).ShouldBeTrue();
            File.Exists(historyPath).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
