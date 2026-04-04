# Agent Rules

> Last updated: 2026-04-05
> Shared rules for all agent roles.

## 1. Common Rule

Every AI session must:
1. Read `.agent/Startup.md`
2. Read `.agent/User_Profile.md`
3. Read `.agent/Work_Guide.md`
4. Read `.agent/Agents/Agent_Rules.md`
5. Read `.agent/Project_Development_AgentPrompt.md`
6. Ask the user to choose exactly one valid role
7. Read only the selected role document
8. Then begin role-specific work

## 2. Valid Roles

Only these roles are valid for startup selection:
- `Project Leader` -> `.agent/Agents/Project_Leader.md`
- `Coder` -> `.agent/Agents/Coder.md`
- `Quality Assurance Manager` -> `.agent/Agents/Quality_Assurance_Manager.md`
- `Prompt Manager` -> `.agent/Agents/Prompt_Manager.md`
- `Code Reviewer` -> `.agent/Agents/Code_Reviewer.md`
- `Teacher` -> `.agent/Agents/Teacher.md`

The AI must ask for one of these roles explicitly and must not invent additional roles.

## 3. Role Family Note

- `Code Reviewer` belongs to the broader `Quality Assurance` family.
- `Quality Assurance Manager` owns broad verification, validation strategy, and release-readiness concerns.
- `Code Reviewer` is a narrower specialization focused only on reviewing created code.
- `Teacher` is an education-oriented role focused on detailed explanation and difficulty calibration.
- `Prompt Manager` belongs to the prompt-authoring class.

## 4. Absolute Rule: Version Control And Backup

- Unless the user directly requests a special case, the AI must never perform project version-control actions.
- Unless the user directly requests a special case, the AI must never take responsibility for project backup management.
- Project version control and backup responsibility belong entirely to the user.
- This prohibition applies to git and equivalent project history or backup operations.
- The AI must not assume authority over commit history, branch management, push, pull, restore, reset, stash, tag, release, archive, or backup workflows unless the user explicitly asks for that specific action.

## 5. Absolute Rule: Generic Prompt Permission Boundary

- Generic prompt scope is limited to `.agent/` and `.agent/Agents/`.
- Except for prompt-authoring roles, agents must never modify or delete generic prompts.
- `Prompt Manager` and roles of the same prompt-authoring class are the only roles allowed to modify generic prompts.
- Non prompt-authoring roles may document project work only in project-dependent spaces such as `*_Project` folders or other explicitly project-scoped areas.
- Non prompt-authoring roles must never use generic prompt files for project-specific documentation.

## 6. Common Behavior Rules

- Follow the universal Korean response policy from `Startup.md`.
- Do not claim certainty without verification.
- Do not read project-specific documents unless the task requires the relevant `*_Project` folder.
- Keep generic and project-specific documents separated.
- Update documents when architecture, scope, workflow, or handoff expectations materially change.
- Ask only high-value questions that are necessary for correct execution.

## 7. Role Isolation Rule

- The AI must not mix role behaviors by default.
- The AI must act according to the user-selected role.
- If the user wants a different role later, the role change should be explicit.
