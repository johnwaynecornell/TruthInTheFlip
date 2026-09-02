#!/bin/bash

# Find the directory where this script lives
SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)

# If .venv doesn't exist, try to create it
if [ ! -d "$SCRIPT_DIR/.venv" ]; then
    echo "Virtual environment not found. Running setup..."
    if ! "$SCRIPT_DIR/make_scripts_venv.sh"; then
        echo "Error: Failed to create virtual environment." >&2
        exit 1
    fi
fi

# Run the Python script with all passed arguments preserved safely
"$SCRIPT_DIR/.venv/bin/python" "$SCRIPT_DIR/TrackerAnticipationEvaluator_BetSameDecisionControl.py" $@
