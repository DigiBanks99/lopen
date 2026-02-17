namespace Lopen.Auth.Tests;

public class ModuleTests
{
    [Fact]
    public void Auth_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Auth", typeof(ModuleTests).Namespace!);
    }
}
