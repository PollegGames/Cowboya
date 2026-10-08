#!/usr/bin/env bash
set -euo pipefail

project_path="${PROJECT_PATH:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"
unity_version="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' "$project_path/ProjectSettings/ProjectVersion.txt" | head -n 1)"
unity_path="${UNITY_PATH:-}"

if [[ -z "$unity_path" ]]; then
    if command -v unity >/dev/null 2>&1; then
        unity_path="$(command -v unity)"
    elif [[ "$(uname -s)" == "Darwin" ]]; then
        unity_path="/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity"
    else
        unity_path="$HOME/Unity/Hub/Editor/$unity_version/Editor/Unity"
    fi
fi

if [[ ! -x "$unity_path" ]]; then
    echo "Unity $unity_version was not found at '$unity_path'. Set UNITY_PATH explicitly." >&2
    exit 1
fi

artifacts_path="${ARTIFACTS_PATH:-$project_path/Logs/CI}"
mkdir -p "$artifacts_path"

"$unity_path" \
    -batchmode \
    -nographics \
    -projectPath "$project_path" \
    -runTests \
    -testPlatform EditMode \
    -testResults "$artifacts_path/editmode-results.xml" \
    -logFile "$artifacts_path/editmode.log"
