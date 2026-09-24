#!/usr/bin/env sh
set -eu
project_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$project_root"

docker compose --profile test config --quiet
rm -rf "$project_root/artifacts"
mkdir -p artifacts

cleanup() {
  status=$?
  trap - EXIT
  docker compose --profile test logs --no-color app db > artifacts/services.log 2>&1 || true
  if ! docker compose --profile test cp tests:/work/artifacts/. artifacts/; then
    echo 'Could not collect test artifacts.' >&2
    if [ "$status" -eq 0 ]; then status=1; fi
  fi
  docker compose --profile test down --volumes --remove-orphans || status=1
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

docker compose --profile test up --build --abort-on-container-exit --exit-code-from tests
