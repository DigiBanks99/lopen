using Microsoft.Extensions.AI;

namespace Lopen.Llm;

/// <summary>
/// Converts Lopen tool definitions to SDK-native AIFunction instances
/// so the Copilot SDK can invoke tool handlers during SendAndWaitAsync.
/// </summary>
internal static class ToolConversion
{
    /// <summary>
    /// Converts a list of <see cref="LopenToolDefinition"/> to SDK <see cref="AIFunction"/> instances.
    /// Tools without a bound handler are excluded.
    /// </summary>
    public static List<AIFunction> ToAiFunctions(IReadOnlyList<LopenToolDefinition> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        return tools
            .Where(t => t.Handler is not null)
            .Select(ToAiFunction)
            .ToList();
    }

    private static AIFunction ToAiFunction(LopenToolDefinition tool)
    {
        // Wrap the Lopen handler delegate so AIFunctionFactory can bind it.
        // The SDK calls the delegate with (string arguments, CancellationToken ct).
        var handler = tool.Handler!;
        return AIFunctionFactory.Create(
            async (string arguments, CancellationToken ct) => await handler(arguments, ct),
            tool.Name,
            tool.Description);
    }
}
