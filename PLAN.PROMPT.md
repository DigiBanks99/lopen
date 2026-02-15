1. Study the SPECIFICATION.md files in the @docs/requirements/core/
2. Study the  @.lopen/jobs-to-be-done.json file if it exists.
2. Look for incomplete or partially complete jobs to be done.
3. Use sub-agents to study the code and look for TODOs, FIXMEs, temporary implementations or other indicators of incomplete work that map back to existing requirements.
4. Verify if the job might already be done (do not assume not implemented). Also ensure it truly is done by checking for tests that prove the implementation works as intended.
5. Create or update the @.lopen/jobs-to-be-done.json document describing the next most important tasks that need to be done to build out lopen limited to 100 jobs.
6. Each line should have an id, a requirement code that maps back to a @docs/requirements/core/SPECIFICATION.md for a requirement module, a brief description for human readability and a status tracking with an optional partial implementation description or issues experienced. Make use of subagents to identify the most important items and to order them by priority.
7. Use subagents to research how to do the open jobs to be done and write them to the appropriate @docs/requirements/core/RESEARCH.md file.
8. If @docs/requirements/core/RESEARCH.md files already exist in the relevant requirement sub-folders, use sub-agents to validate if it is still correct with the codebase and known industry developments; update where necessary or recreate if vastly different.

IMPORTANT:
- Do not make up any requirements
- Use only requirements from SPECIFICATION.md files
- If you find that there are gaps in SPECIFICATION.md, add the missing requirements to the SPECIFICATION.md files in question.
- If a new module is needed, create a new requirement folder in @docs/requirements and add a SPECIFICATION.md file in the new folder. Then update @docs/requirements/README.md to reference the new module.
- The @.github/agents/research.agent.md agent is good at research if you are picking subagents
- Keep the jobs to be done list to a maximum of 100 items. Clean out completed or obsolete jobs to be done if space is needed.
- Ensure all jobs to be done are atomic, actionable, and easily referenceable in a SPECIFICATION.md file.
- Adding tests can be a job to be done if tests are missing for a requirement.
- Fixing build and test failures must be done as high priority jobs to be done.
- Update @docs/requirements/README.md if a module is present in the file structure but missing from the README.md file.
