#!/bin/bash
INPUT=$(cat)
TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name')

if [ "$TOOL_NAME" = "editFiles" ] || [ "$TOOL_NAME" = "createFile" ]; then
  FILES=$(echo "$INPUT" | jq -r '.tool_input.files[]? // .tool_input.path // empty')

  for FILE in $FILES; do
    if [ -f "$FILE" ]; then
      dotnet format "$FILE" 2>/dev/null
    fi
  done
fi

if [ "$TOOL_NAME" = "run_terminal_command" ] || [ "$TOOL_NAME" = "runInTerminal" ]; then
  dotnet format Lopen.slnx 2>/dev/null
fi

echo '{"continue":true}'
