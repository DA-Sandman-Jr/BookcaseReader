#!/usr/bin/env bash
# Auto-formats the *staged* C# files and re-stages them. Invoked from Claude
# Code's PreToolUse:Bash hook in .claude/settings.json so commits made through
# the assistant land formatted. Manual commits get the verify-only safety net
# from .githooks/pre-commit instead.
#
# IMPORTANT: format only the staged files, never the whole solution. Running
# `dotnet format <sln>` unscoped reformats every drifted file in the repo into
# the working tree on each commit — churning the diff with unrelated changes and
# wasting review/triage time. This mirrors the staged-file scoping that
# .githooks/pre-commit already uses for its --verify-no-changes check.
#
# Lives in scripts/ rather than inline in the JSON because Claude Code's hook
# executor was prepending "bash " to the command string; a script path makes
# that prefix harmless (`bash scripts/foo.sh` is a valid invocation).
set -e

if [ -f "$CLAUDE_PROJECT_DIR/scripts/activate-dotnet.sh" ]; then
    # shellcheck source=scripts/activate-dotnet.sh
    . "$CLAUDE_PROJECT_DIR/scripts/activate-dotnet.sh"
fi

SLN=$(ls "$CLAUDE_PROJECT_DIR"/*.sln 2>/dev/null | head -1)

if [ -z "$SLN" ]; then
    echo "[pre-commit] No .sln at repo root — skipping format."
    exit 0
fi

# Staged C# files only (Added/Copied/Modified; Renamed/Deleted excluded).
STAGED_CS=$(git -C "$CLAUDE_PROJECT_DIR" diff --cached --name-only --diff-filter=ACM -- '*.cs' || true)

if [ -z "$STAGED_CS" ]; then
    echo "[pre-commit] No staged C# files — skipping format."
    exit 0
fi

# `dotnet format --include` takes a space-separated path list, so scope the
# format to exactly these files instead of the whole solution.
INCLUDE_ARGS=$(echo "$STAGED_CS" | tr '\n' ' ')
(cd "$CLAUDE_PROJECT_DIR" && dotnet format "$SLN" --include $INCLUDE_ARGS)

# Re-stage whatever the formatter changed.
echo "$STAGED_CS" | xargs -r git -C "$CLAUDE_PROJECT_DIR" add
