1. Ensure you are not on the `main` branch. It is okay to be on a feature branch. Don't create a branch for every task.
2. Study the @.lopen/jobs-to-be-done.json file.
3. Identify the highest priority task that is not yet marked as complete.
4. Study the relevant SPECIFICATION.md file in the corresponding @docs/requirements/<module>/ folder to fully understand the requirement.
5. Verify that the feature is not already completed
6. Update the @.lopen/jobs-to-be-done.json document to reflect any changes in priorities or new tasks that have emerged.
7. Use a subagent to study existing RESEARCH.md files in the relevant requirement sub-folder to gather information on how to implement the task.
8. Use subagents to research how the feature integrates with existing modules and features
9. Use a subagent to output a concise plan in @docs/requirements/IMPLEMENTATION_PLAN.md for the job to be done.
10. If all tasks in @docs/requirements/IMPLEMENTATION_PLAN.md are complete, identify the next most important task to focus on from the jobs-to-be-done.json file. If still none to be done, output a "lopen.loop.done" file to signal completion.
11. If there @docs/requirements/IMPLEMENTATION_PLAN.md contains tasks to be done, implement them in the codebase.
12. Prioritize adding tests before marking a task or job as complete.
13. Document new features or changes to features using the divio model
14. Commit all the changes using conventional commit messages.

IMPORTANT:
- Do not make up any requirements
- Use only existing requirements from SPECIFICATION.md files
- If you find that there are gaps in SPECIFICATION.md, update the SPECIFICATION.md files with the new requirements.
- A job to be done is only done if it can be proven by tests (excludes non-technical tasks, i.e. documentation, design, package updates)
- If a new module is needed, create a new requirement folder in @docs/requirements and add a minimal SPECIFICATION.md file there. Update @docs/requirements/README.md to reference the new module.
- If IMPLEMENTATION_PLAN.md becomes too long, clear out old and completed tasks to keep it concise.
- You don't get to decide that tests are optional or don't add value.
- Run a sub-agent with model gpt-5-mini to verify that all the acceptance criteria have been met before marking a task as done. Be clear on what task was done and where to find the acceptance criteria.
- Run all tests.
- Verify the features added by running the application and using the features where possible.
- If you find a bug, create a new job to be done for the bug fix and prioritize it accordingly. Do not just fix the bug without tracking it as a job to be done.
- Ensure you update the state of each module, component and task in @.lopen/module/<module>/state.json as you work on them. This is crucial for tracking progress and understanding the current state of the project.
