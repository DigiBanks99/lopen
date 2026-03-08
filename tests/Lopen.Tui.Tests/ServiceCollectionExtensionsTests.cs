using Microsoft.Extensions.DependencyInjection;
using Lopen.Tui;

namespace Lopen.Tui.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenTui_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenTui();

        Assert.Same(services, result);
    }
}
