#!/bin/bash

cd "$(dirname "$0")/.."

ITERATION=0

# if first arg is "plan" use PLAN.PROMPT.md
if [ "$1" == "plan" ]; then
    PROMPT_FILE="PLAN.PROMPT.md"

    # Print a message indicating that we are running in plan mode
    echo "------------"
    echo "Mode: PLAN"
    echo "------------"

    echo "Removing previous lopen.loop.done file if it exists..."
    rm -f lopen.loop.done

    # run copilot cli once with --allow-all for the selected prompt file and exit
    copilot -p "$(cat "$PROMPT_FILE")" \
        --allow-all \
        --model claude-opus-4.5 \
        --stream on \
        --no-auto-update\
        --log-level all

    cd -
    exit 0
else
    PROMPT_FILE="BUILD.PROMPT.md"
fi

# Print a message indicating that we are running in build mode
echo "------------"
echo "Mode: BUILD"
echo "------------"

echo "Removing previous lopen.loop.done file if it exists..."
rm -f lopen.loop.done

# loop over copilot cli with --allow-all for the selected prompt file until lopen.loop.done file exists
while [ ! -f lopen.loop.done ]; do
    copilot -p "$(cat "$PROMPT_FILE")" \
        --allow-all \
        --model claude-opus-4.5 \
        --stream on \
        --no-auto-update\
        --log-level all

    # if iteration is not yet initialized, initialize it
    if [ -z "$ITERATION" ]; then
        ITERATION=0
    fi

    ITERATION=$((ITERATION + 1))
    echo "------------------------------"
    echo "Completed iteration $ITERATION"
    echo "------------------------------"
done

cd -
