---
name: write-unit-tests
description: Write well-structured unit tests that conform to standards for the lopen codebase to ensure code quality and reliability.
---

Unit tests are a limited form of testing that are focussed on the integrity of a single unit of code, such as a method or class. They are designed to be fast and isolated, allowing developers to quickly verify that their code behaves as expected under various conditions.

## Test Conventions

1. Test follow the Arrange-Act-Assert pattern
2. We don't add Arrange-Act-Assert comments to the test but use whitespace to separate the sections of the test
3. Tests are independent and can be run in any order
4. Tests are deterministic and produce the same result every time they are run
5. Tests assert outputs for a given set of inputs and conditions
6. Test expectations can consist of multiple assertions
7. Assertions are done with `Shouldly` and not with `Assert` or other assertion libraries, to improve readability and consistency across tests
8. Mocks and stubs are done with `NSubstitute`
9. Logging and telemetry are never tested
  1. Use `NullLogger.Instance` for tests (`Microsoft.Extensions.Logging.Abstractions` namespace)
  2. Use `NullTelemetryClient.Instance` for tests (`Microsoft.IdentityModel.Abstractions` namespace)

For example:

```csharp
[Fact]
public void GivenAnInstruction_WhenRunningInference()
{
    IInferenceAuditor auditor = Substitute.For<IInferenceAuditor>();
    InferenceEngine engine = new(auditor);

    string result = engine.Process("Calculate 10 + 5");

    ItShouldHaveRunTheInference(result, engine);
    ItShouldHaveAuditedTheInference(auditor);
}

private static void ItShouldHaveRunTheInference(string result, InferenceEngine engine)
{
    result.ShouldNotBeEmpty();
    engine.GetInferenceTrace().ShouldContain("Calculate 10 + 5");
}

private static void ItShouldHaveAuditedTheInference(IInferenceAuditor auditor)
{
    auditor
      .Received()
      .Audit("Calculate 10 + 5");
}
```

## Naming Conventions

1. Test names define the criteria
2. Test names do not define the expected result
3. Test names use PascalCase and separate conditions or exceptions with '_'
