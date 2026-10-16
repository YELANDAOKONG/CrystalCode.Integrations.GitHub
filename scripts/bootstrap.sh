#!/usr/bin/env bash
set -euo pipefail
umask 077

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'This action requires a Linux x64 runner.' >&2
  exit 1
fi

for dependency in curl unzip; do
  command -v "$dependency" >/dev/null || { echo "Missing required program: $dependency" >&2; exit 1; }
done

validate_version() {
  [[ "$1" =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]] || { echo 'Invalid release version.' >&2; exit 1; }
}

download_release() {
  local repository="$1" version="$2" asset="$3" destination="$4"
  validate_version "$version"
  local release_path="download/$version"
  if [[ "$version" == latest ]]; then
    release_path='latest/download'
  fi
  curl --fail --location --silent --show-error --retry 3 --connect-timeout 30 --max-time 300 \
    --proto '=https' --proto-redir '=https' \
    "https://github.com/$repository/releases/$release_path/$asset" --output "$destination"
}

: "${RUNNER_TEMP:?RUNNER_TEMP is required}"
task_root="$(mktemp -d "$RUNNER_TEMP/crystal-github.XXXXXXXX")"
download_release YELANDAOKONG/CrystalCode "${CRYSTAL_GITHUB_VERSION:-latest}" \
  CrystalCode-linux-x64.zip "$task_root/crystal.zip"
download_release YELANDAOKONG/CrystalCode.Integrations.GitHub "${CRYSTAL_GITHUB_INTEGRATION_VERSION:-latest}" \
  CrystalCode.Integrations.GitHub.Action-linux-x64.zip "$task_root/integration.zip"
unzip -q "$task_root/crystal.zip" -d "$task_root/crystal"
unzip -q "$task_root/integration.zip" -d "$task_root/integration"
crystal_executable="$task_root/crystal/publish/CrystalCode"
bootstrap_executable="$task_root/integration/bootstrap/CrystalCode.Integrations.GitHub.Bootstrap"
test -f "$crystal_executable" && test -f "$bootstrap_executable"
chmod u+x "$crystal_executable" "$bootstrap_executable"
# Model and tool output is data, not GitHub workflow commands.
command_token="$(cat /proc/sys/kernel/random/uuid)"
printf '::stop-commands::%s\n' "$command_token"
status=0
"$bootstrap_executable" "$crystal_executable" "$task_root/integration" "$task_root" || status=$?
printf '::%s::\n' "$command_token"
exit "$status"
