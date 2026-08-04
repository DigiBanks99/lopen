#!/bin/bash
INPUT=$(cat)
TOOL_NAME=$(echo "$INPUT" | jq -r '.tool_name')

randotnet_format=false
result=""
if [ "$TOOL_NAME" = "editFiles" ] || [ "$TOOL_NAME" = "createFile" ] || [ "$TOOL_NAME" = "replace_string" ] || [ "$TOOL_NAME" = "multiline_replace_string" ] || [ "$TOOL_NAME" = "apply_patch" ]; then
    randotnet_format=true
    result=$(dotnet format Lopen.slnx 2>&1)
fi

# if it failed return the error in the json
if [ $? -ne 0 ]; then
    echo '{"continue":false, "error":"'"$result"'"}'
    exit 0
fi

echo '{"continue":true, "ranFormat":'"$randotnet_format"'}'
