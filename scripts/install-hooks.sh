#!/usr/bin/env bash
# One-time setup: point git at the .githooks/ directory so the pre-commit
# format check runs on every commit. Idempotent.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

git -C "$ROOT_DIR" config core.hooksPath .githooks

# Ensure the hook is executable (mostly relevant on POSIX; Windows git-bash
# honors this bit too).
chmod +x "$ROOT_DIR/.githooks/pre-commit" 2>/dev/null || true

echo "Hooks installed. core.hooksPath = .githooks"
echo "Pre-commit will run 'dotnet format --verify-no-changes' on staged .cs files."
