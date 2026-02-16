---
name: unit-test-guidelines
description: Standardize how to write focused, deterministic unit tests that follow Arrange-Act-Assert, use Shouldly/NSubstitute, and avoid logging or telemetry assertions.
---

# Unit Test Guidelines
Keep unit tests tight, fast, and self-contained so developers can confirm the behavior of a single method or class without needing external services.

## Core conventions
- Structure every test with three logical sections separated by blank lines: **Arrange** prepares collaborators and inputs, **Act** exercises the unit, and **Assert** inspects outcomes.
- Avoid tracing the sections with inline comments; rely on whitespace to make each step stand out.
- Tests must be independent and deterministic—no shared state, no randomness, and no ordering assumptions.
- Only assert observable outputs or interactions for a given setup; skip logging/telemetry assertions entirely (use `NullLogger.Instance` and `NullTelemetryClient.Instance` to satisfy dependencies instead).

## Naming and assertions
- Name tests in PascalCase and describe the triggering conditions, separating clauses with underscores (e.g., `GivenValidPrompt_WhenRunningInference`).
- Do not describe the expected result in the name—let the assertions communicate the outcome.
- Prefer `Shouldly` for all verifications; combine multiple `Should...()` statements in the final section when necessary.

## Collaborators and mocks
- Use `NSubstitute` for doubles (`Substitute.For<...>()`).
- Keep mocks focused on the interactions relevant to a single scenario.
- If you need to satisfy logging or telemetry dependencies, inject `NullLogger.Instance` from `Microsoft.Extensions.Logging.Abstractions` and `NullTelemetryClient.Instance` from `Microsoft.IdentityModel.Abstractions` instead of making them part of the assertions.

## Example
```csharp
[Fact]
public void GivenAnInstruction_WhenRunningInference()
{
    IInferenceAuditor auditor = Substitute.For<IInferenceAuditor>();
    InferenceEngine engine = new(auditor);

    string result = engine.Process("Calculate 10 + 5");

    result.ShouldNotBeEmpty();
    engine.GetInferenceTrace().ShouldContain("Calculate 10 + 5");
    auditor.Received().Audit("Calculate 10 + 5");
}
```
This example keeps the Arrange, Act, and Assert sections distinct with blank lines, uses `Shouldly` for every check, and relies on `NSubstitute` to capture the auditor interaction.
