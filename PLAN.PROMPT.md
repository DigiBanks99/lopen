1. Run the test suite and validate the application is still working using a subagent.
2. Study the modules in `lopen-memory`.
3. Study the state of features in `lopen-memory`.
4. Study the state of tasks in `lopen-memory`.
5. Look for incomplete or partially complete jobs to be done.
6. Use subagents to study the code and look for TODOs, FIXMEs, temporary implementations or other indicators of incomplete work that map back to existing requirements.
7. Use subagents to compare the code against the modules from `lopen-memory`.
8. Verify if jobs that are not marked as complete might already be done (do not assume not implemented).
9. Pick a single module that is not yet complete or needs attention.
10. Determine what would need to be done for the module to be considered complete.
11. Break the module into atomic features and track them in `lopen-memory`.
12. Search for existing research in `lopen-memory` for implementing the module and check if research that exists is stale.
13. If needed, use subagents to research how to implement the atomic features and track the research in `lopen-memory`.
14. Create atomic, actionable tasks with clear acceptance criteria for each feature and track them in `lopen-memory`.

IMPORTANT:
- Do not make up any requirements.
- Use only requirements from `lopen-memory`.
- If you identify gaps, add or update the features or tasks.
- If you find a need for a new module, create a new module in `lopen-memory`.
- Tests must be part of the acceptance criteria for each feature.
- Fixing build and test failures must be done as high priority jobs to be done.
