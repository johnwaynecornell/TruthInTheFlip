#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPORT4_CMD="$1"

TRACKER_PATH="$2"
TRACKER="$(basename "$2")"
WINDOW="$3"


echo REPORT4_CMD $REPORT4_CMD
echo TRACKER_PATH $TRACKER_PATH
echo TRACKER $TRACKER
echo WINDOW $WINDOW


$SCRIPT_DIR/header_help.sh "TRACKER: $TRACKER" "WINDOW: $WINDOW" "$REPORT4_CMD" -window WindowByTotal $WINDOW -print Detailed "$TRACKER_PATH" -grade all -whole -info
