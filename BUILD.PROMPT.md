1. Ensure you are not on the `main` branch. It is okay to be on a feature branch. Don't create a branch for every task.
2. Study the @.lopen/jobs-to-be-done.json file.
3. Study the @.lopen/module/<module>/state.json file for the relevant module to understand the current state of the module and its components.
4. Identify the highest priority job to be done that is not yet marked as complete.
5. Study the relevant SPECIFICATION.md file in the corresponding @docs/requirements/<module>/ folder to fully understand the requirement.
6. Verify that the feature is not already completed using a sub-agent.
7. Update the @.lopen/jobs-to-be-done.json document to reflect any changes in priorities or new tasks that have emerged.
8. Use a subagent to study existing RESEARCH.md files in the relevant requirement sub-folder to gather information on how to implement the task.
9. Use subagents to research how the feature integrates with existing modules and features.
10. Identify the next task to be done for the job you are working on and break it down into atomic, actionable steps if necessary.
11. Update the @.lopen/jobs-to-be-done.json document to reflect the new task.
12. Remember your context window is limited so use sub-agents for development and task completion.
13. Prioritize adding tests before marking a task or job as complete.
14. Document new features or changes to features using the divio model
15. Write the state of the module, component and task to the state store in @.lopen/module/<module>/state.json .
16. Format the code and then run all tests and ensure they pass using a sub-agent.
18. Verify the features added by running the application and using the features where possible using a sub-agent with model gpt-5-mini.
19. Commit all the changes using conventional commit messages.
20. Push the changes to the remote repository.

IMPORTANT:
- Do not make up any requirements
- Use only existing requirements from SPECIFICATION.md files
- If you find that there are gaps in SPECIFICATION.md, update the SPECIFICATION.md files with the new requirements.
- A job to be done is only done if it can be proven by tests (excludes non-technical tasks, i.e. documentation, design, package updates)
- You must fix failing tests before continuing.
- You don't get to decide that tests are optional or don't add value.
- If a new module is needed, create a new requirement folder in @docs/requirements and add a minimal SPECIFICATION.md file there. Update @docs/requirements/README.md to reference the new module.
- Run a sub-agent with model gpt-5-mini to verify that all the acceptance criteria have been met before marking a task as done. Be clear on what task was done and where to find the acceptance criteria.
- Verify the features added by running the application and using the features where possible.
- If you find a bug, create a new job to be done for the bug fix and prioritize it accordingly. Do not just fix the bug without tracking it as a job to be done.
- Ensure you update the state of each module, component and task in @.lopen/module/<module>/state.json as you work on them. This is crucial for tracking progress and understanding the current state of the project.
