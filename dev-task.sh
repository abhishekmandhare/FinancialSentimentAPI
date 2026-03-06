#!/usr/bin/env bash
# dev-task.sh — Kick off an autonomous developer task in the background.
#
# Usage:
#   ./dev-task.sh "#42"                              # from GitHub issue number
#   ./dev-task.sh "Add a GET endpoint for trending"  # from inline description
#
# Launches Claude Code headless. Runs independently from your main session.

set -euo pipefail
export PATH="$PATH:/c/Program Files/GitHub CLI"

if [[ $# -lt 1 ]]; then
  echo "Usage:"
  echo "  ./dev-task.sh \"#42\"                              # from GitHub issue"
  echo "  ./dev-task.sh \"Add a GET endpoint for trending\"  # inline description"
  exit 1
fi

INPUT="$1"
REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

mkdir -p "$REPO_ROOT/.claude/task-logs"

# Detect if input is a GitHub issue number
if [[ "$INPUT" =~ ^#?([0-9]+)$ ]]; then
  ISSUE_NUM="${BASH_REMATCH[1]}"
  echo "Fetching issue #$ISSUE_NUM..."

  ISSUE_TITLE=$(gh issue view "$ISSUE_NUM" --json title -q '.title')
  ISSUE_BODY=$(gh issue view "$ISSUE_NUM" --json body -q '.body')
  ISSUE_LABELS=$(gh issue view "$ISSUE_NUM" --json labels -q '[.labels[].name] | join(", ")')

  TASK="GitHub Issue #$ISSUE_NUM: $ISSUE_TITLE

Description:
$ISSUE_BODY

Labels: $ISSUE_LABELS"

  CLOSE_REF="Closes #$ISSUE_NUM"
  LOG_FILE="$REPO_ROOT/.claude/task-logs/$TIMESTAMP-issue-$ISSUE_NUM.log"
else
  TASK="$INPUT"
  CLOSE_REF=""
  LOG_FILE="$REPO_ROOT/.claude/task-logs/$TIMESTAMP.log"
fi

PROMPT=$(cat <<PROMPT_EOF
You are a senior .NET developer. Complete this task end-to-end and open a PR.

TASK: $TASK

WORKFLOW:
1. Read and understand the relevant code in the codebase.
2. Create a branch from main: feature/<short-desc>, fix/<short-desc>, or chore/<short-desc>.
3. Implement the change following project conventions (records for DTOs, static Create() factories, one class per file, FluentValidation at API boundary).
4. Add or update unit tests (FluentAssertions + NSubstitute, naming: MethodName_Scenario_ExpectedResult).
5. Run: cd $REPO_ROOT && dotnet test Tests/Tests.csproj — all tests must pass.
6. Commit with a descriptive message. End with: Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
7. Push the branch and open a PR with:
   export PATH="\$PATH:/c/Program Files/GitHub CLI"
   gh pr create --title "<concise title>" --body "<summary + test plan>

   $CLOSE_REF"
8. Print the PR URL when done.

ARCHITECTURE RULES:
- Domain: zero external packages
- Application: depends on Domain only, defines interfaces
- Infrastructure: implements interfaces, owns EF Core + HTTP clients
- API: composition root, thin controllers
- Never commit credentials. Never interpolate user input into SQL.

IMPORTANT: Run dotnet test before committing. Do not commit if tests fail.
PROMPT_EOF
)

echo "Starting task: $INPUT"
echo "Log: $LOG_FILE"
echo "---"

env -u CLAUDECODE claude -p "$PROMPT" \
  --allowedTools '*' \
  --permission-mode bypassPermissions \
  --model claude-sonnet-4-6 \
  --output-format stream-json \
  --verbose \
  2>&1 | tee "$LOG_FILE" > /dev/null &

TASK_PID=$!
echo "Running as PID $TASK_PID"
echo "Tail logs: tail -f $LOG_FILE"
echo "Check result: cat $LOG_FILE"
