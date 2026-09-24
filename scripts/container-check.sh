#!/usr/bin/env bash
set -euo pipefail
cd /work
mkdir -p artifacts/allure-results

dotnet format --no-restore --verify-no-changes
node node_modules/prettier/bin/prettier.cjs --check .

set +e
dotnet test --no-build -c Release --settings tests/SupportDesk.Tests/test.runsettings \
  --logger 'trx;LogFileName=tests.trx' --results-directory /work/artifacts/test-results
test_status=$?
node node_modules/allure/cli.js generate artifacts/allure-results --output artifacts/allure-report
report_status=$?
set -e

if [ "$test_status" -ne 0 ]; then
  exit "$test_status"
fi
exit "$report_status"
