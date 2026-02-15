namespace Lopen.Llm;

/// <summary>
/// Scope levels for oracle verification.
/// </summary>
public enum VerificationScope
{
    /// <summary>Verify a single task's diff and tests.</summary>
    Task,

    /// <summary>Verify all tasks in a component.</summary>
    Component,

    /// <summary>Verify the entire module against acceptance criteria.</summary>
    Module,
}
