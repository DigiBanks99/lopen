namespace Lopen.Otel.Tests;

public class ModuleTests
{
    [Fact]
    public void Otel_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Otel", typeof(ModuleTests).Namespace!);
    }
}
