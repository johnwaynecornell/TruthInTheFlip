#!/usr/bin/env bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TRACKER="$(basename "$2")"

$SCRIPT_DIR/header_help.sh "TRACKER: $TRACKER" "WINDOW: 100B" "$1" -window WindowByTotal def -print Detailed "$2" -grade all -whole -info
$SCRIPT_DIR/header_help.sh "TRACKER: $TRACKER" "WINDOW: 10B" "$1" -window WindowByTotal 10000000000 -print Detailed "$2" -grade all -whole -info
