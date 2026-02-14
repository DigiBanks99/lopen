---
name: commit-changes
description: Commit changes to the repository.
---

Changes should be committed to the VCS system as atomic units of work.

1. Ensure that the repository is not on the main branch.
2. Identify all the files that have been changed. Use subagents to assist if necessary.
3. Determine the units of work.
4. For each unit of work:
  1. stage the lines that belong to that unit of work
  2. draft a single line conventional commit message
  3. add a detailed description of the changes for the unit of work
  4. if an issue tracker is being used, link the commit to the relevant issue(s)
5. Push the commits to the remote repository once all the commits have been made.

It is not necessary to create a branch per commit, only that the commits are not made on the main branch.

## Optional extra steps

1. Determine previous changes and commits
2. If a commit is related to a previous commit, link the commits together using the appropriate syntax in the commit message (e.g., "fixes #123" or "relates to #456").
3. Prefer rebase over merge for upstream changes

## IMPORTANT

- Never ever ever merge so multiple parent commits are created. Always rebase or cherry-pick to ensure a clean commit history.
- Every commit message should be prefixed with a conventional commit type (e.g., "feat(xxx):", "fix(yyy):", "docs(zzz):", "refactor(www):", etc.) to indicate the nature of the change.
- Every commit message must have a body that describes the intent and reason for the change, not just what was changed.
