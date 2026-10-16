#!/usr/bin/env bash
# One-time local git setup for this repo: Git LFS + Unity Smart Merge (UnityYAMLMerge).
# Run from the repo root after cloning:  ./scripts/setup-git.sh
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

UNITY_VERSION="$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)"
UNITY_EDITOR_DIR="${UNITY_EDITOR_DIR:-$HOME/Unity/Hub/Editor/$UNITY_VERSION/Editor}"
YAML_MERGE="$UNITY_EDITOR_DIR/Data/Tools/UnityYAMLMerge"

git lfs install --local

if [[ ! -x "$YAML_MERGE" ]]; then
    echo "UnityYAMLMerge not found at: $YAML_MERGE" >&2
    echo "Install Unity $UNITY_VERSION via Unity Hub, or set UNITY_EDITOR_DIR." >&2
    exit 1
fi

git config --local merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
git config --local merge.unityyamlmerge.driver "'$YAML_MERGE' merge -h -p --force %O %B %A %A"
git config --local merge.unityyamlmerge.recursive binary
git config --local mergetool.unityyamlmerge.trustExitCode false
git config --local mergetool.unityyamlmerge.cmd "'$YAML_MERGE' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""

echo "Git LFS and Unity Smart Merge configured (Unity $UNITY_VERSION)."
