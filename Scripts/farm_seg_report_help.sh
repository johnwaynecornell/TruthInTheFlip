#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FARM_CMD="$1"

TRACKER_PATH="$2"
TRACKER="$(basename "$2")"
WINDOW="$3"

$SCRIPT_DIR/header_help.sh "TRACKER: $TRACKER" "WINDOW: $WINDOW" "$FARM_CMD" segment_report All full window by_total "$WINDOW" file "$TRACKER_PATH" full by_total 100B
