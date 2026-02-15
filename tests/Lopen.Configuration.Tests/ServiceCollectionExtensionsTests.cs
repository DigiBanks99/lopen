using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Configuration.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenConfiguration_RegistersLopenOptions()
    {
        var services = new ServiceCollection();
        var options = new LopenOptions();

        services.AddLopenConfiguration(options);

        var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<LopenOptions>();

        Assert.Same(options, resolved);
    }

    [Fact]
    public void AddLopenConfiguration_RegistersAllNestedOptions()
    {
        var services = new ServiceCollection();
        var options = new LopenOptions();

        services.AddLopenConfiguration(options);

        var provider = services.BuildServiceProvider();

        Assert.Same(options.Models, provider.GetRequiredService<ModelOptions>());
        Assert.Same(options.Budget, provider.GetRequiredService<BudgetOptions>());
        Assert.Same(options.Oracle, provider.GetRequiredService<OracleOptions>());
        Assert.Same(options.Workflow, provider.GetRequiredService<WorkflowOptions>());
        Assert.Same(options.Session, provider.GetRequiredService<SessionOptions>());
        Assert.Same(options.Git, provider.GetRequiredService<GitOptions>());
        Assert.Same(options.ToolDiscipline, provider.GetRequiredService<ToolDisciplineOptions>());
        Assert.Same(options.Display, provider.GetRequiredService<DisplayOptions>());
    }

    [Fact]
    public void AddLopenConfiguration_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddLopenConfiguration(new LopenOptions());

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenConfiguration_RegistersBudgetEnforcer()
    {
        var services = new ServiceCollection();
        services.AddLopenConfiguration(new LopenOptions());

        var provider = services.BuildServiceProvider();
        var enforcer = provider.GetRequiredService<IBudgetEnforcer>();

        Assert.IsType<BudgetEnforcer>(enforcer);
    }
}
