---
name: verify-job-complete
description: Verifies if the job to be done is complete
---

1. Study the @docs/requirements/job-to-be-done.json for the job to be done
2. Find the matching requirement in the matching @docs/requirements/<module>/SPECIFICATION.md
3. Verify all acceptance criteria are met
4. Verify tests have been added if this is not a documentation, linting, formatting, package update or design job
5. Run `dotnet build` and verify all passed
6. Run `dotnet test` and verify all passed
7. Run `dotnet-coverage` and verify coverage did not regress
8. If it exists run `lopen test self` and verify it passes
