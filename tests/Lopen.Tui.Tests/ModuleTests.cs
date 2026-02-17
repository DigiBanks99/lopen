namespace Lopen.Tui.Tests;

public class ModuleTests
{
    [Fact]
    public void Tui_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Tui", typeof(ModuleTests).Namespace!);
    }
}
