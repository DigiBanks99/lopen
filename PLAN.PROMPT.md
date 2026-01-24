I want a jobs-to-be-done document describing the next most important tasks that need to be done to build out lopen. It should be placed in @docs\requirements and must be a valid json array.
Each line should have an id, a requirement code that maps back to a SPECIFICATION.md for a requirement, a brief description for human readability and a status tracking with an optional partial implementation description or issues experienced. Make use of subagents to identify the most important items and to order them by priority.
After that has been done, decide on the next most important thing to be done and use up to 50 agents to research how to do it and output a IMPLEMENTATION_PLAN.md inside @docs\requirements. @docs/requirements/IMPLEMENTATION_PLAN.md must be brief but explain what must be done. @docs/requirements/IMPLEMENTATION_PLAN.md might not be correct, so verify.
The additional research must be added to a RESEARCH.md file inside the relevant requirement sub-folder and IMPLEMENTATION_PLAN.md can reference back to that for detailed instructions where needed.
Keep IMPLEMENTATION_PLAN.md short and concise to limit token flooding.
Update AGENTS.md if necessary with learnings about the repo and not about requirements

IMPORTANT:
- Do not make up any requirement codes
- Use only existing ones from SPECIFICATION.md
- If you find that there are gaps in SPECIFICATION.md, add them.
- If a new module is needed, create a new requirement folder in @docs/requirements and add a SPECIFICATION.md file there. Update @docs/requirements/README.md to reference the new module.
