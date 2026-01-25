# Tech Debt - Specification

> Cross-cutting technical debt items and migration tasks

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-TD-001 | Move from FluentAssertions to Shouldly | High | ✅ Complete |

---

## REQ-TD-001: Move from FluentAssertions to Shouldly

### Description

Migrate the repository's test assertion library from FluentAssertions to Shouldly across all test projects to provide clearer failure messages and align with team preferences.

### Rationale

Shouldly offers concise, readable assertions and human-friendly failure output which improves developer productivity when diagnosing test failures.

### Steps

1. Add `Shouldly` NuGet package to all test projects and remove `FluentAssertions` package references.
2. Replace `using FluentAssertions;` usings with `using Shouldly;` where applicable.
3. Convert common patterns (e.g., `actual.Should().Be(expected)` -> `actual.ShouldBe(expected)`) and document mapping for other idioms.
4. Update test helpers and custom assertion wrappers to use Shouldly.
5. Run the full test suite and fix compilation or semantic differences.
6. Update CI configuration and developer docs to reference Shouldly.

### Acceptance Criteria

- All test projects compile and run with Shouldly-only references (no FluentAssertions remains).

### Test Cases

| ID | Description | Expected |
|----|-------------|----------|
| TC-TD-001-01 | Replace basic equality assertions | Tests compile and behave equivalently |
| TC-TD-001-02 | Replace collection assertions | Tests compile and behave equivalently |

### Implementation Notes

- Some FluentAssertions APIs (e.g., complex equivalency options) may not have direct Shouldly equivalents; prefer explicit assertions in those cases.
- Consider using Roslyn-based codemods for safe automated replacements; otherwise, provide recommended regex patterns and manual steps in the migration guide.

### Risks

- Manual fixes required for complex assertions could introduce regressions; run full test suite and review changes carefully.
- Assertion-message differences may change test failure diagnostics; ensure devs are briefed on Shouldly idioms.

### Estimate

- Manual migration and fixes: 1-2 days
- Optional tooling (codemod): 1-2 additional days

### Deliverables

- `docs/requirements/tech-debt/SPECIFICATION.md` (this document)
- Tests green on main with Shouldly-only dependencies

---

## Other Tech Debt Tasks (Future)

- REQ-TD-002: Audit and reduce reflection usage
- REQ-TD-003: Remove deprecated APIs and update package references
