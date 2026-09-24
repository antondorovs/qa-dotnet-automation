$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
$status = 1
try {
  docker compose --profile test config --quiet
  if ($LASTEXITCODE -ne 0) { throw 'Invalid Compose configuration.' }
  $artifacts = Join-Path $projectRoot 'artifacts'
  if (Test-Path -LiteralPath $artifacts) {
    if ((Resolve-Path -LiteralPath $artifacts).Path -ne $artifacts) {
      throw 'Unexpected artifacts directory.'
    }
    Remove-Item -LiteralPath $artifacts -Recurse -Force
  }
  New-Item -ItemType Directory -Path $artifacts | Out-Null
  try {
    docker compose --profile test up --build --abort-on-container-exit --exit-code-from tests
    $status = $LASTEXITCODE
  }
  finally {
    docker compose --profile test logs --no-color app db 2>&1 | Set-Content (Join-Path $artifacts 'services.log')
    docker compose --profile test cp tests:/work/artifacts/. artifacts/
    if ($LASTEXITCODE -ne 0) {
      Write-Warning 'Could not collect test artifacts.'
      if ($status -eq 0) { $status = 1 }
    }
    docker compose --profile test down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0) { $status = 1 }
  }
}
finally {
  Pop-Location
}
exit $status
