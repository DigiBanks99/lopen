1. Run the test suite and validate the application is still working using a subagent.
2. Study the SPECIFICATION.md files in the @docs/requirements/core/
3. Study the  @.lopen/jobs-to-be-done.json file if it exists.
4. Study the @.lopen/module folders for state of completion.
5. Look for incomplete or partially complete jobs to be done.
6. Use sub-agents to study the code and look for TODOs, FIXMEs, temporary implementations or other indicators of incomplete work that map back to existing requirements.
7. Verify if the job might already be done (do not assume not implemented). Also ensure it truly is done by checking for tests that prove the implementation works as intended.
8. Pick a single module that is not yet complete or needs attention.
9. Determine what would need to be done for the module to be considered complete.
10. Create a @.lopen/jobs-to-be-done.json with the tasks identified as needed to complete the module feature.
11. Create a @.lopen/module/<module>/state.json with the state of the task.
12. Use subagents to research how to do the open jobs to be done and write them to the appropriate @docs/requirements/core/RESEARCH.md file.
13. If @docs/requirements/core/RESEARCH.md files already exist in the relevant requirement sub-folders, use sub-agents to validate if it is still correct with the codebase and known industry developments; update where necessary or recreate if vastly different.
14. Use a sub-agent to determine if tests are failing. If they are prioritize fixing the tests and the underlying issues as high priority jobs to be done.

IMPORTANT:
- Do not make up any requirements
- Use only requirements from SPECIFICATION.md files
- If you find that there are gaps in SPECIFICATION.md, create a @docs/requirements/<module>/OPEN_QUESTIONS.md file.
- If you find a need for a new module, create a @docs/requirements/<module>/DRAFT.md file with a brief explanation of the need.
- Keep the jobs to be done list to a maximum of 100 items. Clean out completed or obsolete jobs to be done if space is needed.
- Ensure all jobs to be done are atomic, actionable, and easily referenceable in a SPECIFICATION.md file.
- Adding tests can be a job to be done if tests are missing for a requirement.
- Fixing build and test failures must be done as high priority jobs to be done.
- Update @docs/requirements/README.md if a module is present in the file structure but missing from the README.md file.
