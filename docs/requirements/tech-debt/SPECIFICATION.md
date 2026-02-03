# Tech Debt - Specification

> Cross-cutting technical debt items and migration tasks

## Requirements

| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| REQ-TD-001 | Move from FluentAssertions to Shouldly | High | ⚪ Not Started |
| REQ-TD-002 | Documentation Sync - Update README.md status | Medium | ⚪ Not Started |
| REQ-TD-003 | Specification Checkbox Sync - Update acceptance criteria | Medium | ⚪ Not Started |

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

- REQ-TD-004: Audit and reduce reflection usage
- REQ-TD-005: Remove deprecated APIs and update package references

---

## REQ-TD-002: Documentation Sync - Update README.md Status

### Description

Keep docs/requirements/README.md status indicators in sync with actual implementation status. When features are completed, update the status from 🔴 Not Started or 🟡 In Progress to ⚪ Not Started.

### Acceptance Criteria

- [ ] Phase 4 Loop status updated to Complete
- [ ] Phase 5 Testing status updated to Complete
- [ ] All phase status indicators match actual implementation

### Notes

Discovered during JTBD audit 2026-01-27: README.md showed Loop Phase 4 as "Not Started" when it was fully implemented with tests.

---

## REQ-TD-003: Specification Checkbox Sync - Update Acceptance Criteria

### Description

Update unchecked acceptance criteria checkboxes in SPECIFICATION.md files when features are implemented. Many features show `- [ ]` (unchecked) when they should show `- [ ]` (checked).

### Affected Files

- `docs/requirements/loop/SPECIFICATION.md` - ~40 criteria to update
- `docs/requirements/tui/SPECIFICATION.md` - ~40 criteria to update
- `docs/requirements/testing/SPECIFICATION.md` - ~5 criteria to update

### Acceptance Criteria

- [ ] All implemented features have checked boxes
- [ ] Test case status columns show ⏸️ for passing tests

### Notes

Research agents created detailed gap analysis documents identifying which checkboxes need updating:
- `docs/requirements/loop/IMPLEMENTATION_GAPS.md`
- `docs/requirements/tui/GAP_ANALYSIS_SUMMARY.md`
