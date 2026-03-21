#!/bin/bash
INPUT=$(cat)
TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name')

randotnet_format=false
if [ "$TOOL_NAME" = "editFiles" ] || [ "$TOOL_NAME" = "createFile" ] || [ "$TOOL_NAME" = "replace_string" ] || [ "$TOOL_NAME" = "multiline_replace_string" ] || [ "$TOOL_NAME" = "apply_patch" ]; then
    randotnet_format=true
    dotnet format Lopen.slnx 2>/dev/null
fi

echo '{"continue":true, "ranFormat":'"$randotnet_format"'}'
