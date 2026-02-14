---
name: draft-spec
description: Draft a well-written module specification following the Lopen specification pattern.
---

Draft a module specification that follows the canonical Lopen specification pattern. The result must be a complete, well-structured `SPECIFICATION.md` ready for human review.

## Instructions

1. Identify the module name and its high-level purpose from the user's input or conversation context.
2. Interview the user to gather requirements if the input is vague. Ask focused, one-at-a-time questions.
3. Create the specification file at `docs/requirements/<module>/SPECIFICATION.md`.
4. Follow the **Required Structure** below exactly.
5. Write acceptance criteria as concrete, verifiable conditions — not vague aspirations.
6. Identify dependencies on other modules (check `docs/requirements/` subfolders) and external libraries.
7. Define skills and hooks that are specific to this module's verification needs.

## Required Structure

Every specification must include these sections in this order:

```markdown
---
name: <module-name>
description: <one-line summary>
---

# <Module Name> Specification

## Overview
What this module does, why it exists, and its design principles.

## [Domain-Specific Sections]
The behavioral specification of the module. Use H2 for major topics,
H3 for subsections. Be specific — define data models, interfaces,
constraints, error cases, and edge cases.

## Acceptance Criteria
A checkbox list of concrete, verifiable conditions that must be true
for the module to be considered complete. Each criterion should be
testable — either by automated tools or by inspection.

## Dependencies
Other modules, libraries, APIs, or external systems this module requires.
Use a list format with brief rationale for each dependency.

## Skills & Hooks
Module-specific tools, skills, or verification commands. Define what
must be checked and the commands to check it.

## Notes
Open questions, implementation caveats, or future considerations.

## References
Links to related specifications, external docs, or prior research.
```

## Quality Checklist

Before finalizing the specification, verify:

- [ ] YAML frontmatter has `name` and `description`
- [ ] Overview clearly states purpose and scope
- [ ] Acceptance criteria are concrete and verifiable (not vague)
- [ ] Each acceptance criterion can be checked by a tool, test, or inspection
- [ ] Dependencies list is complete (modules, libraries, APIs)
- [ ] Skills & Hooks define verification commands appropriate to the module
- [ ] No implementation details — the spec says *what*, not *how*
- [ ] Cross-references to related specs use relative paths

## Anti-Patterns to Avoid

- **Vague criteria**: "Code is well-tested" → instead: "Unit test coverage exists for all public methods"
- **Implementation leakage**: "Use a HashMap for lookups" → instead: "Lookups must be O(1)"
- **Missing error cases**: Always define what happens on failure, not just the happy path
- **Unbounded scope**: Each section should have clear boundaries; flag anything out of scope in Notes
