$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
  $branch = git branch --show-current
  if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') { throw 'Switch to main before publishing.' }
  $changes = git status --porcelain
  if ($LASTEXITCODE -ne 0 -or $changes) { throw 'Commit or resolve local changes before publishing.' }
  $commit = git rev-parse HEAD
  if ($LASTEXITCODE -ne 0) { throw 'Cannot read HEAD.' }
  $failures = @()
  foreach ($remote in @('origin', 'gitlab')) {
    git push $remote "${commit}:refs/heads/main"
    if ($LASTEXITCODE -ne 0) { $failures += $remote }
  }
  foreach ($remote in @('origin', 'gitlab')) {
    $remoteRef = git ls-remote $remote refs/heads/main
    if ($LASTEXITCODE -ne 0 -or -not $remoteRef -or ($remoteRef -split '\s+')[0] -ne $commit) {
      $failures += $remote
    }
  }
  if ($failures.Count -gt 0) { throw "Publication incomplete: $($failures -join ', ')" }
  Write-Output "Both remotes point to $commit"
}
finally {
  Pop-Location
}
