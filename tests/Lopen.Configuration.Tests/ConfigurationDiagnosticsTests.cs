using Microsoft.Extensions.Configuration;

namespace Lopen.Configuration.Tests;

public class ConfigurationDiagnosticsTests
{
    [Fact]
    public void GetEntries_ReturnsEntriesWithSources()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Models:Planning"] = "gpt-5",
                ["Workflow:MaxIterations"] = "50"
            })
            .Build();

        var entries = ConfigurationDiagnostics.GetEntries(config);

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Key == "Models:Planning" && e.Value == "gpt-5");
        Assert.Contains(entries, e => e.Key == "Workflow:MaxIterations" && e.Value == "50");
    }

    [Fact]
    public void GetEntries_SkipsNullValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Key1"] = "value",
                ["Key2"] = null
            })
            .Build();

        var entries = ConfigurationDiagnostics.GetEntries(config);

        Assert.Single(entries);
        Assert.Equal("Key1", entries[0].Key);
    }

    [Fact]
    public void GetEntries_IdentifiesMemoryProviderAsCLIOverride()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Models:Planning"] = "gpt-5"
            })
            .Build();

        var entries = ConfigurationDiagnostics.GetEntries(config);

        Assert.Single(entries);
        Assert.Equal("CLI Override", entries[0].Source);
    }

    [Fact]
    public void GetEntries_IdentifiesJsonProvider()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"Models": {"Planning": "gpt-5"}}""");

            var config = new ConfigurationBuilder()
                .AddJsonFile(tempFile, optional: false)
                .Build();

            var entries = ConfigurationDiagnostics.GetEntries(config);

            Assert.Contains(entries, e => e.Key == "Models:Planning" && e.Value == "gpt-5");
            var planningEntry = entries.First(e => e.Key == "Models:Planning");
            Assert.Contains(Path.GetFileName(tempFile), planningEntry.Source);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Format_ReturnsTableWithHeaders()
    {
        var entries = new List<ConfigurationEntry>
        {
            new("Models:Planning", "gpt-5", "CLI Override"),
            new("Workflow:MaxIterations", "50", "project.json")
        };

        var output = ConfigurationDiagnostics.Format(entries);

        Assert.Contains("Setting", output);
        Assert.Contains("Value", output);
        Assert.Contains("Source", output);
        Assert.Contains("Models:Planning", output);
        Assert.Contains("gpt-5", output);
    }

    [Fact]
    public void Format_EmptyEntries_ReturnsMessage()
    {
        var output = ConfigurationDiagnostics.Format([]);

        Assert.Equal("No configuration entries found.", output);
    }

    [Fact]
    public void GetEntries_NullConfig_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigurationDiagnostics.GetEntries(null!));
    }

    [Fact]
    public void GetEntries_HigherPriorityProviderWins()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"Key": "from-json"}""");

            var config = new ConfigurationBuilder()
                .AddJsonFile(tempFile, optional: false)
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Key"] = "from-override"
                })
                .Build();

            var entries = ConfigurationDiagnostics.GetEntries(config);

            var entry = entries.First(e => e.Key == "Key");
            Assert.Equal("from-override", entry.Value);
            Assert.Equal("CLI Override", entry.Source);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
