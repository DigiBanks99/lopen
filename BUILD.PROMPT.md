1. Ensure you are not on the `main` branch.
2. Study `lopen-memory` for a feature that is not complete.
3. Identify the most important open/partially completed task to be done.
4. Study the module feature to understand the current state of the feature and its tasks. It contains information on previous steps and actions taken.
5. Use a subagent to verify that the feature is not already completed by studying the code and comparing it to the acceptance criteria of the module, the feature and the task.
7. Update the task, module and feature to reflect any changes in priorities or new tasks that have emerged.
8. Use a subagent to study existing research to gather information on how to implement the task.
9. Use subagents to research how the feature integrates with existing modules and features.
10. Decide on a task to work on.
11. Update the task, feature and module to reflect state of the task selected.
12. Use as many subagents as needed to complete the task.
13. Use only one subagent to verify the implementation.
14. Commit all the changes using conventional commit messages.
15. Push the changes to the remote repository.

IMPORTANT:
- If all the work on the selected feature is done, output a blank file called lopen.loop.done in the root directory and don't attempt any further actions.
- Do not make up any requirements.
- Stick to a single feature.
- It is okay to be on a feature branch. Don't create a branch for every task.
- Remember your context window is limited so use sub-agents for development and task completion.
- Prioritize adding tests before marking a task or job as complete.
- A job to be done is only done if it can be proven by tests (excludes non-technical tasks, i.e. documentation, design, package updates)
- You must fix failing tests before continuing.
- You don't get to decide that tests are optional or don't add value.
- Use the feature end-to-end before marking it as done.
- Use a subagent to verify end-to-end code usage from Lopen CLI to the module.
- If you find a bug not caused by the work you are doing, create a new task for the feature and provide as much detail as you can without attempting to fix it. Only fix the bug if it is blocking you from completing the task you are working on.
