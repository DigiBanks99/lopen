using Spectre.Console;

namespace Lopen.Core.Testing;

/// <summary>
/// Interactive test selector using Spectre.Console prompts.
/// </summary>
public sealed class SpectreInteractiveTestSelector : IInteractiveTestSelector
{
    private readonly IAnsiConsole _console;
    
    /// <summary>
    /// Creates a new SpectreInteractiveTestSelector.
    /// </summary>
    /// <param name="console">Spectre.Console console (defaults to AnsiConsole.Console).</param>
    public SpectreInteractiveTestSelector(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }
    
    /// <inheritdoc />
    public InteractiveTestSelection SelectTests(
        IEnumerable<ITestCase> availableTests,
        string defaultModel,
        CancellationToken cancellationToken = default)
    {
        var testList = availableTests.ToList();
        
        if (testList.Count == 0)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = false
            };
        }
        
        // Group tests by suite
        var testsBySuite = testList
            .GroupBy(t => t.Suite)
            .OrderBy(g => g.Key)
            .ToList();
        
        // Build multi-selection prompt with grouped choices
        var prompt = new MultiSelectionPrompt<string>()
            .Title("[bold cyan]Select tests to run[/] (space to toggle, enter to confirm)")
            .PageSize(15)
            .Required()
            .InstructionsText("[grey](Use arrow keys to navigate, space to select, enter to confirm)[/]");
        
        // Map test ID to test case for lookup
        var testLookup = testList.ToDictionary(t => FormatTestChoice(t), t => t);
        
        // Add grouped choices
        foreach (var suiteGroup in testsBySuite)
        {
            var suiteTests = suiteGroup.Select(t => FormatTestChoice(t)).ToArray();
            prompt.AddChoiceGroup(
                $"[yellow]{suiteGroup.Key}[/]",
                suiteTests);
            
            // Pre-select all tests
            foreach (var test in suiteTests)
            {
                prompt.Select(test);
            }
        }
        
        // Show test selection prompt
        List<string> selectedTestIds;
        try
        {
            selectedTestIds = _console.Prompt(prompt);
        }
        catch (OperationCanceledException)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        if (selectedTestIds.Count == 0)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        // Map selected strings back to test cases
        var selectedTests = selectedTestIds
            .Where(id => testLookup.ContainsKey(id))
            .Select(id => testLookup[id])
            .ToList();
        
        // Prompt for model selection
        var models = new[]
        {
            "gpt-5-mini",
            "gpt-5",
            "gpt-4.1",
            "claude-sonnet-4"
        };
        
        var modelPrompt = new SelectionPrompt<string>()
            .Title("[bold cyan]Select model for tests[/]")
            .AddChoices(models);
        
        // Pre-select default model if in list
        if (models.Contains(defaultModel))
        {
            // SelectionPrompt doesn't have Select, but we can reorder
            var reordered = new[] { defaultModel }
                .Concat(models.Where(m => m != defaultModel))
                .ToArray();
            modelPrompt = new SelectionPrompt<string>()
                .Title("[bold cyan]Select model for tests[/]")
                .AddChoices(reordered);
        }
        
        string selectedModel;
        try
        {
            selectedModel = _console.Prompt(modelPrompt);
        }
        catch (OperationCanceledException)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        // Confirm selection
        var confirmPrompt = new ConfirmationPrompt(
            $"[bold]Run [green]{selectedTests.Count}[/] test(s) with model [cyan]{selectedModel}[/]?[/]")
        {
            DefaultValue = true
        };
        
        bool confirmed;
        try
        {
            confirmed = _console.Prompt(confirmPrompt);
        }
        catch (OperationCanceledException)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        if (!confirmed)
        {
            return new InteractiveTestSelection
            {
                Tests = Array.Empty<ITestCase>(),
                Model = defaultModel,
                Cancelled = true
            };
        }
        
        return new InteractiveTestSelection
        {
            Tests = selectedTests,
            Model = selectedModel,
            Cancelled = false
        };
    }
    
    private static string FormatTestChoice(ITestCase test)
    {
        return $"{test.TestId}: {test.Description}";
    }
}
