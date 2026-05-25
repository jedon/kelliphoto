# Copy install-github-runner.sh to Debian and run it (prompts for SSH password if needed).
# Usage: .\scripts\deploy\install-github-runner-remote.ps1
#        .\scripts\deploy\install-github-runner-remote.ps1 -SshHost 142.4.216.160 -User jedon

param(
    [string]$SshHost = "142.4.216.160",
    [string]$User = "jedon"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$installScript = Join-Path $repoRoot "scripts\deploy\install-github-runner.sh"

Write-Host "Fetching runner registration token (expires in ~1 hour)..."
$token = (gh api repos/jedon/kelliphoto/actions/runners/registration-token --method POST | ConvertFrom-Json).token

$remote = "${User}@${SshHost}"
Write-Host "Connecting to $remote ..."
Write-Host "You may be prompted for your SSH password."

# LF-only script for bash on Linux
$content = [IO.File]::ReadAllText($installScript) -replace "`r`n", "`n"
$temp = [IO.Path]::GetTempFileName()
[IO.File]::WriteAllText($temp, $content)

scp $temp "${remote}:~/install-github-runner.sh"
if ($LASTEXITCODE -ne 0) { throw "scp failed (is $SshHost reachable on port 22 from this network?)" }
Remove-Item $temp -Force

ssh $remote "chmod +x ~/install-github-runner.sh && RUNNER_TOKEN='$token' bash ~/install-github-runner.sh"
if ($LASTEXITCODE -ne 0) { throw "ssh install failed" }

Write-Host ""
Write-Host "Next: GitHub repo -> Settings -> Actions -> Variables"
Write-Host "  ENABLE_DEPLOY = true"
Write-Host "  DEPLOY_RUNNER = self-hosted"
