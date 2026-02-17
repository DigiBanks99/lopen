namespace Lopen.Llm.Tests;

public class ModuleTests
{
    [Fact]
    public void Llm_Namespace_Exists()
    {
        Assert.StartsWith("Lopen.Llm", typeof(ModuleTests).Namespace!);
    }
}
