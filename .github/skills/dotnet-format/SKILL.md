---
name: dotnet-format
description: Enforce .NET formatting with `dotnet format` when preparing commits/PRs, responding to formatting violations, or whenever whitespace/style fixes are requested across solutions, projects, or folders.
---

# Dotnet Format

## Quick start

- Confirm the SDK version has `dotnet format` (SDK 6+ ships it by default) or install/update it globally: `dotnet tool install dotnet-format --global` or `dotnet tool update dotnet-format --global`.
- Run a complete formatting pass from the repository root so the tool can resolve all projects:  
  ```bash
  dotnet format --solution Lopen.slnx --fix-whitespace --fix-style --fix-analyzers --verbosity minimal
  ```
- If you only want to check for formatting drift without applying changes, add `--check` or `--verify-no-changes` and treat a non-zero exit as a cue to re-run without `--check`.

## Workflow

1. **Decide the scope** - pick `--solution`, `--project`, or `--folder` depending on the files you touched. When targeting changed files, pass the relative paths with `--include` or combine with `git diff --name-only`.
2. **Run a dry pass** - use `dotnet format --check --verbosity minimal` so it lists violations without mutating files; capture the exit code to determine if work is needed.
3. **Apply fixes** - rerun without `--check` to let the tool write changes for whitespace, style, and prompted analyzers. Keep the same scope arguments so you touch the same files.
4. **Review the diff** - run `git status` or `git diff --stat` to confirm the touched files match expectations, then stage only the files that should be committed.
5. **Re-verify before committing** - run `dotnet format --verify-no-changes` with the same scope to ensure everything is now compliant; it should exit with 0 if there are no formatting violations left.

## Customizing formatting

- `.editorconfig` drives most formatting behaviors. Point people to `src/.editorconfig` or repo-wide configurations and remind them that `dotnet format` strictly follows those rules.
- Use `--include`/`--exclude` to fine-tune the directories or files you want `dotnet format` to touch. You can combine glob patterns like `--include src/**/*.cs --exclude src/Generated/**` when focusing on hand-edited code.
- Add `--severity info` if you want the tool to report informational diagnostics (useful when you are experimenting with new rules) or `--severity error` to limit to the strictest violations.
- When need to target a specific runtime, pair with `--framework net10.0` (or the project's highest target) so the formatter resolves analyzers from the matching SDK.
- For heavy repositories, prefer running on a narrower `--folder` scope (e.g., `dotnet format --folder src/Lopen.Core`) and `--verbosity minimal` so you get actionable output fast.

## Troubleshooting

- If `dotnet format` is missing, install it globally or invoke it as a local tool (`dotnet tool install dotnet-format --local`).
- When the tool fails because a project targets a preview SDK, specify that SDK via `DOTNET_ROOT` or the matching `--framework` flag so analyzers load correctly.
- If the formatter cannot run because projects are out of sync, run `dotnet restore` first or rely on `dotnet format` to restore automatically (it already restores unless `--no-restore` is set).
- When `.editorconfig` changes seem ignored, double-check that there is no higher-priority config overriding it; run `dotnet format --diagnostics` to see which rules are being evaluated.
- To integrate into CI or a pre-commit hook, run `dotnet format --check` and fail fast when the output shows files that need formatting.

## Verification tips

- After formatting, add `git diff --stat` to your review checklist so you can see how many files changed and confirm that only formatting updates landed.
- Encourage teammates to run `dotnet format --verify-no-changes` as a gate before pushing to avoid surprise formatting commits triggered by CI.
- When troubleshooting stubborn files, inspect the tool's output and re-run the same command with `--verbosity detailed` to understand which rule blocked a fix.
