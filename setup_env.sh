#!/usr/bin/env bash
# First-time environment check for Cowboya.
#
# The script never modifies the project: packages are pinned by
# Packages/manifest.json and Packages/packages-lock.json, and the editor
# version is read from ProjectSettings/ProjectVersion.txt.
#
# Usage:
#   ./setup_env.sh               check the environment
#   ./setup_env.sh --run-tests   check, then run the Edit Mode tests
#
# Set UNITY_PATH when Unity is installed outside the Unity Hub default folder.
set -euo pipefail

project_path="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
version_file="$project_path/ProjectSettings/ProjectVersion.txt"
status=0

ok()   { echo "  [ok]   $*"; }
warn() { echo "  [warn] $*"; }
fail() { echo "  [fail] $*"; status=1; }

echo "Cowboya environment check"

# 1) Required editor version
if [[ ! -f "$version_file" ]]; then
    echo "ProjectVersion.txt not found: run this script from the Cowboya repository." >&2
    exit 1
fi
unity_version="$(sed -n 's/^m_EditorVersion:[[:space:]]*//p' "$version_file" | head -n 1 | tr -d '\r')"
ok "Project requires Unity $unity_version"

# 2) Git
if command -v git >/dev/null 2>&1; then
    ok "git $(git --version | awk '{print $3}')"
else
    fail "git is not installed"
fi

# 3) Unity editor
unity_path="${UNITY_PATH:-}"
if [[ -z "$unity_path" ]]; then
    case "$(uname -s)" in
        Darwin)
            unity_path="/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents/MacOS/Unity" ;;
        MINGW*|MSYS*|CYGWIN*)
            unity_path="/c/Program Files/Unity/Hub/Editor/$unity_version/Editor/Unity.exe" ;;
        *)
            unity_path="$HOME/Unity/Hub/Editor/$unity_version/Editor/Unity" ;;
    esac
fi
if [[ -x "$unity_path" ]]; then
    ok "Unity found at $unity_path"
else
    fail "Unity $unity_version not found at '$unity_path'. Install it with Unity Hub or set UNITY_PATH."
fi

# 4) Optional: .NET SDK for building the solution outside Unity
if command -v dotnet >/dev/null 2>&1; then
    ok "dotnet $(dotnet --version)"
else
    warn "dotnet SDK not found (optional, only needed to build Cowboya.sln outside Unity)"
fi

# 5) Packages are restored by Unity itself on first open
if [[ -f "$project_path/Packages/packages-lock.json" ]]; then
    ok "Packages are pinned by Packages/packages-lock.json (Unity restores them on first open)"
else
    warn "Packages/packages-lock.json is missing: package versions are not pinned"
fi

if [[ $status -ne 0 ]]; then
    echo "Environment check failed."
    exit $status
fi

echo "Environment ready. Open the project with Unity Hub, or run the tests:"
echo "  ./Tools/run-editmode-tests.sh"
echo "  powershell -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1"

if [[ "${1:-}" == "--run-tests" ]]; then
    UNITY_PATH="$unity_path" "$project_path/Tools/run-editmode-tests.sh"
fi
