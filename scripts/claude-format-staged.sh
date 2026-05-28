#!/usr/bin/env bash
# Auto-formats staged C# files and re-stages them. Invoked from Claude Code's
# PreToolUse:Bash hook in .claude/settings.json so commits made through the
# assistant land formatted. Manual commits get the verify-only safety net from
# .githooks/pre-commit instead.
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

(cd "$CLAUDE_PROJECT_DIR" && dotnet format "$SLN")
git -C "$CLAUDE_PROJECT_DIR" diff --cached --name-only -z -- '*.cs' \
    | xargs -0 -r git -C "$CLAUDE_PROJECT_DIR" add
