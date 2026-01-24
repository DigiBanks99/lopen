Ensure you are not on the `main` branch.
Read the @docs/requirements/IMPLEMENTATION_PLAN.md file to understand the next steps required for development.
Update the @docs/requirements/jobs-to-be-done.json document to reflect any changes in priorities or new tasks that have emerged.
If all tasks in @docs/requirements/IMPLEMENTATION_PLAN.md are complete, identify the next most important task to focus on from the jobs-to-be-done.json file. If still none to be done, output a "lopen.loop.done" file to signal completion.
Research might already exist in the RESEARCH.md files within the relevant requirement sub-folders; reference these where applicable to avoid duplication.
Use sub-agents to validate that the task matches existing requirements from the SPECIFICATION.md files in the respective modules. If it does not match a requirement, add clarification job to be done and update the SPECIFICATION.md accordingly.
Use up to 50 agents to research how to implement the identified task, ensuring that the research is thorough and covers all necessary aspects.
Prioritize adding tests before marking a task as complete.
Commit all the changes using conventional commit messages.

IMPORTANT:
- If you learn anything about working in this repo add it to AGENTS.md
- Do not make up any requirement codes
- Use only existing ones from SPECIFICATION.md
- If you find that there are gaps in SPECIFICATION.md, add them.
- A job to be done is only done if it can be proven by tests (excludes non-technical tasks, i.e. documentation, design, package updates)
- If a new module is needed, create a new requirement folder in @docs/requirements and add a minimal SPECIFICATION.md file there. Update @docs/requirements/README.md to reference the new module.
- If IMPLEMENTATION_PLAN.md becomes too long, clear out old and completed tasks to keep it concise.
- You don't get to decide that tests are optional or don't add value.
