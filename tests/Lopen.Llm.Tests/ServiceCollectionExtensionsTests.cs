using Lopen.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var lopenOptions = new LopenOptions();
        services.AddSingleton(Options.Create(lopenOptions));
        services.AddSingleton(lopenOptions.Oracle);
        services.AddLopenLlm();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddLopenLlm_RegistersILlmService()
    {
        var provider = BuildProvider();
        try
        {
            var service = provider.GetService<ILlmService>();

            Assert.NotNull(service);
            Assert.IsType<CopilotLlmService>(service);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIModelSelector()
    {
        var provider = BuildProvider();
        try
        {
            var selector = provider.GetService<IModelSelector>();

            Assert.NotNull(selector);
            Assert.IsType<DefaultModelSelector>(selector);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersITokenTracker()
    {
        var provider = BuildProvider();
        try
        {
            var tracker = provider.GetService<ITokenTracker>();

            Assert.NotNull(tracker);
            Assert.IsType<InMemoryTokenTracker>(tracker);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public void AddLopenLlm_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLopenLlm();

        Assert.Same(services, result);
    }

    [Fact]
    public async Task AddLopenLlm_ILlmService_IsSingleton()
    {
        var provider = BuildProvider();
        try
        {
            var first = provider.GetRequiredService<ILlmService>();
            var second = provider.GetRequiredService<ILlmService>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_IModelSelector_IsSingleton()
    {
        var provider = BuildProvider();
        try
        {
            var first = provider.GetRequiredService<IModelSelector>();
            var second = provider.GetRequiredService<IModelSelector>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_ITokenTracker_IsSingleton()
    {
        var provider = BuildProvider();
        try
        {
            var first = provider.GetRequiredService<ITokenTracker>();
            var second = provider.GetRequiredService<ITokenTracker>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIToolRegistry()
    {
        var provider = BuildProvider();
        try
        {
            var registry = provider.GetService<IToolRegistry>();

            Assert.NotNull(registry);
            Assert.IsType<DefaultToolRegistry>(registry);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIPromptBuilder()
    {
        var provider = BuildProvider();
        try
        {
            var builder = provider.GetService<IPromptBuilder>();

            Assert.NotNull(builder);
            Assert.IsType<DefaultPromptBuilder>(builder);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIVerificationTracker()
    {
        var provider = BuildProvider();
        try
        {
            var tracker = provider.GetService<IVerificationTracker>();

            Assert.NotNull(tracker);
            Assert.IsType<VerificationTracker>(tracker);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIOracleVerifier()
    {
        var provider = BuildProvider();
        try
        {
            var verifier = provider.GetService<IOracleVerifier>();

            Assert.NotNull(verifier);
            Assert.IsType<OracleVerifier>(verifier);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_IOracleVerifier_IsSingleton()
    {
        var provider = BuildProvider();
        try
        {
            var first = provider.GetRequiredService<IOracleVerifier>();
            var second = provider.GetRequiredService<IOracleVerifier>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIGitHubTokenProvider()
    {
        var provider = BuildProvider();
        try
        {
            var tokenProvider = provider.GetService<IGitHubTokenProvider>();

            Assert.NotNull(tokenProvider);
            Assert.IsType<NullGitHubTokenProvider>(tokenProvider);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersICopilotClientProvider()
    {
        var provider = BuildProvider();
        try
        {
            var clientProvider = provider.GetService<ICopilotClientProvider>();

            Assert.NotNull(clientProvider);
            Assert.IsType<CopilotClientProvider>(clientProvider);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_CustomTokenProvider_IsUsed()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var lopenOptions = new LopenOptions();
        services.AddSingleton(Options.Create(lopenOptions));
        services.AddSingleton(lopenOptions.Oracle);
        services.AddSingleton<IGitHubTokenProvider>(new TestTokenProvider("test-token"));
        services.AddLopenLlm();
        var provider = services.BuildServiceProvider();
        try
        {
            var tokenProvider = provider.GetRequiredService<IGitHubTokenProvider>();

            Assert.IsType<TestTokenProvider>(tokenProvider);
            Assert.Equal("test-token", tokenProvider.GetToken());
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    private sealed class TestTokenProvider(string token) : IGitHubTokenProvider
    {
        public string? GetToken() => token;
    }
}
