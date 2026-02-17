namespace Lopen.Storage.Tests;

public class SessionIdTests
{
    [Fact]
    public void Generate_CreatesSessionId_WithCorrectProperties()
    {
        var date = new DateOnly(2026, 2, 14);
        var id = SessionId.Generate("auth", date, 1);

        Assert.Equal("auth", id.Module);
        Assert.Equal(date, id.Date);
        Assert.Equal(1, id.Counter);
    }

    [Fact]
    public void Generate_NormalizesModuleToLowerCase()
    {
        var id = SessionId.Generate("Auth", new DateOnly(2026, 2, 14), 1);

        Assert.Equal("auth", id.Module);
    }

    [Fact]
    public void Generate_ThrowsOnNullModule()
    {
        Assert.Throws<ArgumentNullException>(() =>
            SessionId.Generate(null!, new DateOnly(2026, 2, 14), 1));
    }

    [Fact]
    public void Generate_ThrowsOnEmptyModule()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionId.Generate("", new DateOnly(2026, 2, 14), 1));
    }

    [Fact]
    public void Generate_ThrowsOnZeroCounter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SessionId.Generate("auth", new DateOnly(2026, 2, 14), 0));
    }

    [Fact]
    public void Generate_ThrowsOnNegativeCounter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SessionId.Generate("auth", new DateOnly(2026, 2, 14), -1));
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var id = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 3);

        Assert.Equal("auth-20260214-3", id.ToString());
    }

    [Fact]
    public void Parse_ValidSessionId_ReturnsCorrectProperties()
    {
        var id = SessionId.Parse("auth-20260214-1");

        Assert.Equal("auth", id.Module);
        Assert.Equal(new DateOnly(2026, 2, 14), id.Date);
        Assert.Equal(1, id.Counter);
    }

    [Fact]
    public void Parse_MultiWordModule_ParsesCorrectly()
    {
        var id = SessionId.Parse("my-module-20260214-5");

        Assert.Equal("my-module", id.Module);
        Assert.Equal(new DateOnly(2026, 2, 14), id.Date);
        Assert.Equal(5, id.Counter);
    }

    [Fact]
    public void Parse_ThrowsOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => SessionId.Parse(null!));
    }

    [Fact]
    public void Parse_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => SessionId.Parse(""));
    }

    [Fact]
    public void Parse_ThrowsOnInvalidFormat()
    {
        Assert.Throws<FormatException>(() => SessionId.Parse("invalid"));
    }

    [Fact]
    public void Parse_ThrowsOnInvalidDate()
    {
        Assert.Throws<FormatException>(() => SessionId.Parse("auth-99991399-1"));
    }

    [Fact]
    public void TryParse_ValidInput_ReturnsSessionId()
    {
        var result = SessionId.TryParse("core-20260214-2");

        Assert.NotNull(result);
        Assert.Equal("core", result.Module);
        Assert.Equal(2, result.Counter);
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsNull()
    {
        Assert.Null(SessionId.TryParse("invalid"));
    }

    [Fact]
    public void TryParse_NullInput_ReturnsNull()
    {
        Assert.Null(SessionId.TryParse(null));
    }

    [Fact]
    public void TryParse_EmptyInput_ReturnsNull()
    {
        Assert.Null(SessionId.TryParse(""));
    }

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var id1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var id2 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

        Assert.Equal(id1, id2);
        Assert.True(id1 == id2);
    }

    [Fact]
    public void Equals_DifferentCounter_ReturnsFalse()
    {
        var id1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var id2 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 2);

        Assert.NotEqual(id1, id2);
        Assert.True(id1 != id2);
    }

    [Fact]
    public void Equals_DifferentModule_ReturnsFalse()
    {
        var id1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var id2 = SessionId.Generate("core", new DateOnly(2026, 2, 14), 1);

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GetHashCode_SameValues_ReturnsSameHash()
    {
        var id1 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);
        var id2 = SessionId.Generate("auth", new DateOnly(2026, 2, 14), 1);

        Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
    }

    [Fact]
    public void Parse_RoundTrips_WithToString()
    {
        var original = SessionId.Generate("storage", new DateOnly(2026, 3, 1), 42);
        var parsed = SessionId.Parse(original.ToString());

        Assert.Equal(original, parsed);
    }
}
