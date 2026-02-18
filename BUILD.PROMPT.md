1. Ensure you are not on the `main` branch. It is okay to be on a feature branch. Don't create a branch for every task.
2. Study the @.lopen/jobs-to-be-done.json file.
3. Identify the most important open/partially completed task to be done.
4. Study the @.lopen/module/<module>/state.json file for the relevant module to understand the current state of the module and its components. It contains information on previous steps and actions taken.
5. Study the relevant SPECIFICATION.md file in the corresponding @docs/requirements/<module>/ folder to fully understand the requirement.
6. Verify that the feature is not already completed using a sub-agent by studying the code.
7. Update the @.lopen/jobs-to-be-done.json document to reflect any changes in priorities or new tasks that have emerged.
8. Use a subagent to study existing RESEARCH.md files in the relevant requirement sub-folder to gather information on how to implement the task.
9. Use subagents to research how the feature integrates with existing modules and features.
10. Update the @.lopen/jobs-to-be-done.json document to reflect the new task.
11. Remember your context window is limited so use sub-agents for development and task completion.
12. Prioritize adding tests before marking a task or job as complete.
13. Document new features or changes to features using the divio model
14. Write the state of the module, component and task to the state store in @.lopen/module/<module>/state.json .
15. Format the code and then run all tests and ensure they pass using a sub-agent.
16. Verify the features added by running the application and using the features where possible using a sub-agent with model gpt-5-mini.
17. Commit all the changes using conventional commit messages.
18. Push the changes to the remote repository.

IMPORTANT:
- Do not make up any requirements
- Use only existing requirements from SPECIFICATION.md files
- A job to be done is only done if it can be proven by tests (excludes non-technical tasks, i.e. documentation, design, package updates)
- You must fix failing tests before continuing.
- You don't get to decide that tests are optional or don't add value.
- Run a sub-agent with model gpt-5-mini to verify that all the acceptance criteria have been met before marking a task as done. Be clear on what task was done and where to find the acceptance criteria.
- Verify the features added by running the application and using the features where possible.
- If you find a bug, create a new job to be done for the bug fix and prioritize it accordingly. Do not just fix the bug without tracking it as a job to be done.
- Ensure you update the state of each module, component and task in @.lopen/module/<module>/state.json as you work on them with context on what was done and what was learned. This is crucial for tracking progress and understanding the current state of the project.
