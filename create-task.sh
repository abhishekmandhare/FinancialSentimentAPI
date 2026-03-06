#!/usr/bin/env bash
# create-task.sh — Create a GitHub issue and optionally kick off the dev agent.
#
# Usage:
#   ./create-task.sh "Add trending symbols endpoint"                    # create issue only
#   ./create-task.sh "Add trending symbols endpoint" --run              # create + run dev agent
#   ./create-task.sh "Fix pagination bug" --label bug                   # with label
#   ./create-task.sh "Fix pagination bug" --label bug --run             # label + run

set -euo pipefail
export PATH="$PATH:/c/Program Files/GitHub CLI"

RUN_AGENT=false
LABEL="task"
TITLE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --run)    RUN_AGENT=true; shift ;;
    --label)  LABEL="$2"; shift 2 ;;
    *)        TITLE="$1"; shift ;;
  esac
done

if [[ -z "$TITLE" ]]; then
  echo "Usage: ./create-task.sh \"<task title>\" [--label bug|feature|chore|task] [--run]"
  exit 1
fi

# Create the issue
ISSUE_URL=$(gh issue create --title "$TITLE" --label "$LABEL" --body "Created via create-task.sh" 2>&1)
ISSUE_NUM=$(echo "$ISSUE_URL" | grep -oP '\d+$')

echo "Created: $ISSUE_URL"

if [[ "$RUN_AGENT" == true ]]; then
  echo "Launching dev agent for #$ISSUE_NUM..."
  REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
  "$REPO_ROOT/dev-task.sh" "#$ISSUE_NUM"
fi
