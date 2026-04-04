# Work Guide

> Last updated: 2026-04-05
> Universal work procedure for AI sessions across projects.

## 1. Document Layers

- `.agent/Startup.md`: canonical startup entrypoint
- `.agent/Startup.HumanReadable.md`: human-readable synchronized mirror of startup
- `.agent/User_Profile.md`: universal communication profile
- `.agent/Work_Guide.md`: universal work procedure
- `.agent/Agents/Agent_Rules.md`: shared agent behavior rules
- `.agent/Agents/*.md`: role-specific behavior prompts
- `.agent/*_Project/*.md`: project-specific source-of-truth documents

## 2. Standard Workflow

### Phase 1. Startup
- Read the mandatory startup documents in the order defined by `Startup.md`.
- Ask the user to choose exactly one valid role defined in `.agent/Agents/`.
- Read only the selected role document before role-specific work begins.

### Phase 2. Sync
- Re-read the relevant prompt and project documents before substantial work.
- Check whether outdated plans or contradictory notes exist.
- Verify repository reality before making factual claims about files, code, or project state.

### Phase 3. Plan
- Clarify the current state, target state, and next action.
- Keep generic prompt layers separate from project-specific documentation.
- Place project-specific planning material only under `*_Project` folders.

### Phase 4. Execute
- Implement or edit in a way consistent with the selected role.
- Keep project documents synchronized with meaningful architectural or scope changes.
- Prefer concrete, reviewable changes over vague documentation.
- If the selected role is `Prompt Manager`, it may maintain generic prompts under `.agent/` and `.agent/Agents/`.
- If the selected role is not a prompt-authoring role, do not modify or delete generic prompts.
- If the selected role is not a prompt-authoring role and documentation is needed, write only in `*_Project` folders or other explicitly project-scoped spaces.
- If the selected role is `Code Reviewer`, restrict work to code review activity unless the user explicitly changes the role.
- If the selected role is `Teacher`, prioritize detailed educational explanation matched to the inferred user level.
- Unless the user directly requests a special case, do not perform git or equivalent version-control actions.
- Version control and backup are the user's responsibility, not the AI's default responsibility.

### Phase 5. Verify
- Run tests or validation when feasible.
- If validation is limited, state that limitation clearly.
- For document work, verify internal consistency, repository alignment, and handoff usability.
- For startup prompts, keep `Startup.md` and `Startup.HumanReadable.md` semantically aligned.

### Phase 6. Document
- Update the minimal set of affected documents.
- Preserve role boundaries and document boundaries.
- Keep startup prompts generic and project prompts project-specific.

## 3. Writing Rules

- One document should have one primary responsibility.
- Historical notes and current plans must be distinguishable.
- Do not write project-specific assumptions into generic prompts.
- When writing HumanReadable mirrors or summary prompts, keep the main policy aligned with the canonical prompt.
