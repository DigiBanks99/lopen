namespace Lopen.Configuration.Tests;

public class ModuleTests
{
    [Fact]
    public void Configuration_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Configuration", typeof(ModuleTests).Namespace!);
    }
}
