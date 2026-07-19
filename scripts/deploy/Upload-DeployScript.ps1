# Upload deploy scripts to the server via SCP (creates remote dirs first).
# Usage:
#   .\scripts\deploy\Upload-DeployScript.ps1
#   .\scripts\deploy\Upload-DeployScript.ps1 -SshHost 142.4.216.160 -IdentityFile ~\.ssh\kelliphoto_gha

param(
    [string]$SshHost = "142.4.216.160",
    [string]$User = "jedon",
    [string]$RemoteDir = "/home/jedon/kelli.photo/scripts/deploy",
    [string]$IdentityFile = "$env:USERPROFILE\.ssh\kelliphoto_gha"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$files = @(
    "remote-deploy.sh",
    "install-github-runner.sh"
) | ForEach-Object { Join-Path $repoRoot "scripts\deploy\$_" }

foreach ($f in $files) {
    if (-not (Test-Path $f)) { throw "Missing: $f" }
}

$remote = "${User}@${SshHost}"
# Operator must have the deploy host key in ~/.ssh/known_hosts before running (StrictHostKeyChecking=yes).
$sshArgs = @("-o", "StrictHostKeyChecking=yes")
if (Test-Path $IdentityFile) {
    $sshArgs += @("-i", $IdentityFile)
}

Write-Host "Creating remote directory: $RemoteDir"
& ssh @sshArgs $remote "mkdir -p '$RemoteDir'"

foreach ($localPath in $files) {
    $name = Split-Path $localPath -Leaf
    # Destination must be an existing directory (trailing slash), not a file path.
    $dest = "${remote}:${RemoteDir}/"
    Write-Host "Uploading $name -> $dest"
    & scp @sshArgs $localPath $dest
    if ($LASTEXITCODE -ne 0) { throw "scp failed for $name" }
}

& ssh @sshArgs $remote "chmod +x '$RemoteDir'/*.sh && ls -la '$RemoteDir'"
if ($LASTEXITCODE -ne 0) { throw "remote chmod/list failed" }

Write-Host "Done."
