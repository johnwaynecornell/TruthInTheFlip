#!/bin/bash

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)

if [ ! -d "$SCRIPT_DIR/.venv" ]; then
    "$SCRIPT_DIR/make_scripts_venv.sh"
fi


"$SCRIPT_DIR/.venv/bin/python" "$SCRIPT_DIR/CompareTrackerTrueZ.py" $@
