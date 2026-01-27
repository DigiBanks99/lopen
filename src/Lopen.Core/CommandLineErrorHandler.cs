namespace Lopen.Core;

/// <summary>
/// Represents a parse error from command-line parsing.
/// </summary>
public record ParseErrorInfo(string Message);

/// <summary>
/// Handles command-line parsing errors and routes them through IErrorRenderer.
/// </summary>
public class CommandLineErrorHandler
{
    private readonly IErrorRenderer _errorRenderer;
    private readonly IReadOnlyList<string> _availableCommands;

    public CommandLineErrorHandler(IErrorRenderer errorRenderer, IEnumerable<string> availableCommands)
    {
        _errorRenderer = errorRenderer ?? throw new ArgumentNullException(nameof(errorRenderer));
        _availableCommands = availableCommands?.ToList() ?? new List<string>();
    }

    /// <summary>
    /// Handles parse errors from command-line parsing.
    /// </summary>
    /// <param name="errors">The parse errors.</param>
    /// <param name="commandTokens">Command tokens for context.</param>
    /// <returns>Exit code for the application.</returns>
    public int HandleParseErrors(IEnumerable<ParseErrorInfo> errors, IEnumerable<string>? commandTokens = null)
    {
        var errorList = errors.ToList();
        if (errorList.Count == 0)
        {
            return 0;
        }

        var tokens = commandTokens?.ToList() ?? new List<string>();

        foreach (var error in errorList)
        {
            var errorInfo = AnalyzeError(error, tokens);
            _errorRenderer.RenderError(errorInfo);
        }

        return ExitCodes.InvalidArguments;
    }

    private ErrorInfo AnalyzeError(ParseErrorInfo error, IReadOnlyList<string> tokens)
    {
        var message = error.Message;

        // Detect unrecognized option errors (check first as --options are also "unrecognized commands")
        if (IsUnrecognizedOptionError(message, out var option))
        {
            return new ErrorInfo
            {
                Title = "Invalid option",
                Message = message,
                TryCommand = GetHelpCommandForContext(tokens),
                Severity = ErrorSeverity.Validation
            };
        }

        // Detect unknown command errors
        if (IsUnknownCommandError(message, out var unknownToken))
        {
            var suggestions = GetSimilarCommands(unknownToken);
            return new ErrorInfo
            {
                Title = "Invalid command",
                Message = $"Command '{unknownToken}' not found",
                Suggestions = suggestions,
                TryCommand = "lopen --help",
                Severity = ErrorSeverity.Validation
            };
        }

        // Detect required argument missing
        if (IsRequiredArgumentMissing(message))
        {
            return new ErrorInfo
            {
                Title = "Missing argument",
                Message = message,
                TryCommand = GetHelpCommandForContext(tokens),
                Severity = ErrorSeverity.Validation
            };
        }

        // Generic parse error
        return new ErrorInfo
        {
            Title = "Command Error",
            Message = message,
            TryCommand = "lopen --help",
            Severity = ErrorSeverity.Error
        };
    }

    private static bool IsUnknownCommandError(string message, out string unknownToken)
    {
        unknownToken = "";
        
        // System.CommandLine 2.0 format: "Unrecognized command or argument 'xyz'"
        const string prefix = "Unrecognized command or argument '";
        if (message.StartsWith(prefix) && message.EndsWith("'."))
        {
            unknownToken = message[prefix.Length..^2];
            return true;
        }
        if (message.StartsWith(prefix) && message.EndsWith("'"))
        {
            unknownToken = message[prefix.Length..^1];
            return true;
        }

        return false;
    }

    private static bool IsRequiredArgumentMissing(string message)
    {
        return message.Contains("Required argument missing") ||
               message.Contains("is required") ||
               message.Contains("Option '--") && message.Contains("is required");
    }

    private static bool IsUnrecognizedOptionError(string message, out string option)
    {
        option = "";
        // "Unrecognized command or argument '--xyz'"
        const string prefix = "Unrecognized command or argument '--";
        if (message.StartsWith(prefix) && message.EndsWith("'."))
        {
            option = "--" + message[prefix.Length..^2];
            return true;
        }
        if (message.StartsWith(prefix) && message.EndsWith("'"))
        {
            option = "--" + message[prefix.Length..^1];
            return true;
        }
        return false;
    }

    private IReadOnlyList<string> GetSimilarCommands(string unknownCommand)
    {
        if (string.IsNullOrEmpty(unknownCommand))
        {
            return Array.Empty<string>();
        }

        return _availableCommands
            .Select(cmd => new { Command = cmd, Distance = LevenshteinDistance(unknownCommand.ToLowerInvariant(), cmd.ToLowerInvariant()) })
            .Where(x => x.Distance <= 3) // Only suggest if close enough
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Command)
            .Take(3)
            .Select(x => x.Command)
            .ToList();
    }

    private static string GetHelpCommandForContext(IReadOnlyList<string> tokens)
    {
        var commandTokens = tokens
            .TakeWhile(t => !t.StartsWith("-"))
            .ToList();

        if (commandTokens.Count > 0)
        {
            return $"lopen {string.Join(" ", commandTokens)} --help";
        }
        return "lopen --help";
    }

    private static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        for (var i = 0; i <= sourceLength; i++) distance[i, 0] = i;
        for (var j = 0; j <= targetLength; j++) distance[0, j] = j;

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost);
            }
        }

        return distance[sourceLength, targetLength];
    }
}
