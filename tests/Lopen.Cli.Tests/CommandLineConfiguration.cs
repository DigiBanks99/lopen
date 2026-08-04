using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

namespace Lopen.Cli.Tests;

internal sealed class CommandLineConfiguration
{
    private readonly Command _rootCommand;
    private readonly ParserConfiguration _parserConfiguration;
    private readonly InvocationConfiguration _invocationConfiguration;

    public CommandLineConfiguration(Command rootCommand)
    {
        _rootCommand = rootCommand ?? throw new ArgumentNullException(nameof(rootCommand));
        _parserConfiguration = new ParserConfiguration();
        _invocationConfiguration = new InvocationConfiguration();
    }

    public TextWriter Output
    {
        get => _invocationConfiguration.Output;
        set => _invocationConfiguration.Output = value;
    }

    public TextWriter Error
    {
        get => _invocationConfiguration.Error;
        set => _invocationConfiguration.Error = value;
    }

    public Task<int> InvokeAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        ParseResult parseResult = CommandLineParser.Parse(_rootCommand, args, _parserConfiguration);
        return parseResult.InvokeAsync(_invocationConfiguration, cancellationToken);
    }
}
