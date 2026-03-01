╰─❯ bat scripts/pre-commit.sh --style=plain
#!/bin/bash

INPUT=$(cat)
TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name')
TOOL_INPUT=$(echo "$INPUT" | jq -r '.tool_input')

# Only run the pre-commit checks if the tool being used is "runTerminalCommand" or "run_in_terminal"
if [[ "$TOOL_NAME" != "runTerminalCommand" && "$TOOL_NAME" != "run_in_terminal" ]]; then
    echo '{"continue":true}'
    exit 0
fi

# Only run if TOOL_INPUT starts with or contains "git commit <anything>"
if [[ "$TOOL_INPUT" != *"git commit"* ]]; then
    echo '{"continue":true}'
    exit 0
fi

echo "Running pre-commit checks..."
echo "Restoring..."
dotnet restore Lopen.slnx
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Restore failed.", "systemMessage": "Operation blocked by pre-commit hook: restore failed. Please run `dotnet restore` to fix restore issues."}'
    exit 1
fi

echo "Building..."
dotnet build Lopen.slnx --no-restore --verbosity normal
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Build failed.", "systemMessage": "Operation blocked by pre-commit hook: build failed. Please run `dotnet build` to fix build issues."}'
    exit 1
fi

echo "Formatting..."
dotnet format Lopen.slnx
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Code formatting failed.", "systemMessage": "Operation blocked by pre-commit hook: code formatting issues detected. Please run `dotnet format` to fix formatting."}'
    exit 1
fi

echo "Linting..."
dotnet format analyzers Lopen.slnx
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Linting failed.", "systemMessage": "Operation blocked by pre-commit hook: linting issues detected. Please run `dotnet format analyzers Lopen.slnx` to fix linting errors."}'
    exit 1
fi

echo "Running tests..."
dotnet test Lopen.slnx --no-build --verbosity normal
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Tests failed.", "systemMessage": "Operation blocked by pre-commit hook: test failures detected. Please run `dotnet test Lopen.slnx` to fix test errors."}'
    exit 1
fi

echo "Running cargo audit..."
dotnet outdated -f Lopen.slnx
if [ $? -ne 0 ]; then
    echo '{"continue":false, "stopReason": "Security audit failed.", "systemMessage": "Operation blocked by pre-commit hook: security vulnerabilities detected. Please run `cargo audit` to fix security issues."}'
    exit 1
fi

echo '{"continue":true}'
