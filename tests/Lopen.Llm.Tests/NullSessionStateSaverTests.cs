namespace Lopen.Llm.Tests;

public class NullSessionStateSaverTests
{
    [Fact]
    public async Task SaveAsync_CompletesSuccessfully()
    {
        var saver = new NullSessionStateSaver();
        await saver.SaveAsync();
    }
}
