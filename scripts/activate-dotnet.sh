#!/usr/bin/env bash
# Adds the .NET SDK to PATH on Windows machines where the dotnet install
# location isn't already on PATH for non-login shells (e.g. Git Bash).
# Sourced by .githooks/pre-commit and scripts/claude-format-staged.sh.

if ! command -v dotnet >/dev/null 2>&1; then
    for candidate in "/c/Program Files/dotnet" "C:/Program Files/dotnet"; do
        if [ -x "$candidate/dotnet.exe" ] || [ -x "$candidate/dotnet" ]; then
            export PATH="$candidate:$PATH"
            break
        fi
    done
fi
