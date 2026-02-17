using System.CommandLine;
using Lopen.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lopen.Commands;

/// <summary>
/// Defines the 'config' command group with the 'show' subcommand.
/// </summary>
public static class ConfigCommand
{
    public static Command Create(IServiceProvider services, TextWriter? output = null, TextWriter? error = null)
    {
        var stdout = output ?? Console.Out;
        var stderr = error ?? Console.Error;

        var config = new Command("config", "Configuration management");

        config.Add(CreateShowCommand(services, stdout, stderr));

        return config;
    }

    private static Command CreateShowCommand(IServiceProvider services, TextWriter stdout, TextWriter stderr)
    {
        var show = new Command("show", "Display resolved configuration with sources");
        var jsonOption = new Option<bool>("--json") { Description = "Output as machine-readable JSON" };
        show.Add(jsonOption);

        show.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var configRoot = services.GetRequiredService<IConfigurationRoot>();
                var entries = ConfigurationDiagnostics.GetEntries(configRoot);
                var useJson = parseResult.GetValue(jsonOption);

                var formatted = useJson
                    ? ConfigurationDiagnostics.FormatJson(entries)
                    : ConfigurationDiagnostics.Format(entries);

                await stdout.WriteLineAsync(formatted);
                return 0;
            }
            catch (Exception ex)
            {
                await stderr.WriteLineAsync(ex.Message);
                return 1;
            }
        });
        return show;
    }
}
