---
name: dotnet-test-verbosity
description: Run `dotnet test` with a predictable verbosity level so the LLM can read the output, spot the failing tests, and help fix them; use when diagnosing test failures or confirming a fix before a PR.
---

# Verbose dotnet test diagnostics

1. **Scope the run** – use `git status`/`git diff --name-only` to see which test projects may be impacted, then target the nearest `.csproj` (or the `tests/` folder) instead of blindly running the entire solution.
2. **Run tests with console verbosity detailed** – execute the test project with a logger that captures the failure stack trace without needing external tooling. Example:

    ```bash
    dotnet test src/Lopen.Core.Tests/Lopen.Core.Tests.csproj \
      --no-restore \
      --no-build \
      --logger:"console" \
      --results-directory artifacts/test-results \
      --verbosity minimal | tee artifacts/test-output.txt
    ```

    - `--logger:"console"` lets the LLM see every failed assertion, stack trace, and inner exception.
    - Pipe through `tee` (or redirect) so you can paste the tail of the output into the chat when seeking help.
    - Use `--no-build`/`--no-restore` once dependencies are in place to keep the focus on test results.

3. **Capture the failure summary** – after the run, highlight the block that includes `Failed tests:` and the `Summary` table; the `artifacts/test-output.txt` file is perfect for sharing with the LLM because it preserves the verbose console log without truncation.
4. **Identify flaky or filtered tests** – if only one test failed, re-run with `--filter FullyQualifiedName~TestName` so the LLM can see the same failure repeatedly and know the exact test case to patch.
5. Don't grep for failed or succeeded output, but read the lines. The dotnet CLI will keep the context small on your behalf.

## Troubleshooting tips

- If the failure message is too terse even with the detailed logger, add `--diag artifacts/diag.log` to capture a diagnostic log that contains internal stack traces and dependency resolution details.
- If dotnet test exits before reaching the failure (e.g., build errors), run `dotnet build` on the same project first and paste the build diagnostics into the chat so the LLM has the complete context.
- When a test takes a long time, limit the verbosity to `normal` yet keep the console logger detailed; the `--logger:"console"` value is usually enough for the LLM to see the failing assertions while keeping the stream readable.
- For enabled crash dumps, set `DOTNET_DiagnosticPorts` or add `--blame` so you preserve dumps for post-mortem analysis.

Always treat non-zero exits as signals to capture the relevant failing snippet and share it. the LLM can then correlate the test name, exception type, and stack trace to suggest the fix.
