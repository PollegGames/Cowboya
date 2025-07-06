#!/usr/bin/env bash
set -euo pipefail

# Detect Linux vs Windows Git Bash
IS_LINUX=false
if [[ "$(uname -s 2>/dev/null)" == "Linux" ]]; then
  IS_LINUX=true
fi

# 1) (Linux only) Install prerequisites
if $IS_LINUX && command -v apt-get &>/dev/null; then
  echo "Installing system prerequisites via apt-get…"
  sudo apt-get update
  sudo apt-get install -y git wget jq unzip libgtk-3-0 libnss3 libasound2 libxss1
else
  echo "Skipping system-package install (not on a Linux apt-get host)."
fi

# 2) (Linux only) Install Unity Hub CLI
if $IS_LINUX && ! command -v unity-hub &>/dev/null; then
  echo "Installing Unity Hub CLI…"
  wget -qO UnityHub.AppImage "https://public-cdn.cloud.unity3d.com/hub/prod/UnityHub.AppImage"
  chmod +x UnityHub.AppImage
  sudo mv UnityHub.AppImage /usr/local/bin/unity-hub
else
  echo "Skipping Unity Hub CLI install (already installed or not Linux)."
fi

# 3) (Linux only) Install Unity Editor via Unity Hub
UNITY_VER="2021.3.8f1"
if $IS_LINUX && command -v unity-hub &>/dev/null; then
  echo "Installing Unity Editor ${UNITY_VER} via Hub…"
  unity-hub -- --headless install --version "$UNITY_VER" --accept-license
else
  echo "Skipping Unity Editor install (use Unity Hub on Windows to install ${UNITY_VER})."
fi

# 4) Patch manifest.json to include Test Framework & your UPM deps
MANIFEST="Packages/manifest.json"
if [[ -f "$MANIFEST" ]]; then
  echo "Patching $MANIFEST for test-framework and other packages…"
  jq '.dependencies += {
      "com.unity.test-framework": "1.1.29",
      "com.unity.textmeshpro": "3.0.6",
      "com.unity.addressables": "1.21.15",
      "com.github.yourorg.pathfinding": "https://github.com/yourorg/pathfinding.git#1.2.0"
    }' "$MANIFEST" > "$MANIFEST.tmp" && mv "$MANIFEST.tmp" "$MANIFEST"
else
  echo "ERROR: $MANIFEST not found!"
  exit 1
fi

# 5) Pre-download all packages via Unity CLI (batchmode)
if command -v unity-editor &>/dev/null; then
  echo "Restoring UPM packages with Unity batchmode…"
  unity-editor \
    -batchmode \
    -projectPath "$(pwd)" \
    -quit
else
  echo "Skipping Unity batch restore (install Unity Editor on Windows and add it to your PATH as 'unity-editor')."
fi

echo "✅ Environment setup script finished."
