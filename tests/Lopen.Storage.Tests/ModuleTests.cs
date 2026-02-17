namespace Lopen.Storage.Tests;

public class ModuleTests
{
    [Fact]
    public void Storage_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Storage", typeof(ModuleTests).Namespace!);
    }
}
