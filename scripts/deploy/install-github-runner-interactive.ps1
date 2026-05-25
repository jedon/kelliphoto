# Install GitHub runner via SSH (prompts for password — never store passwords in files).
# Usage: .\scripts\deploy\install-github-runner-interactive.ps1

param(
    [string]$SshHost = "142.4.216.160",
    [string]$User = "jedon"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$installScript = Join-Path $repoRoot "scripts\deploy\install-github-runner.sh"

Write-Host "Fetching runner registration token..."
$token = (gh api repos/jedon/kelliphoto/actions/runners/registration-token --method POST | ConvertFrom-Json).token

$password = Read-Host "SSH password for ${User}@${SshHost}" -AsSecureString
$cred = New-Object PSCredential($User, $password)

Import-Module Posh-SSH
$content = ([IO.File]::ReadAllText($installScript) -replace "`r`n", "`n")
$temp = [IO.Path]::GetTempFileName()
[IO.File]::WriteAllText($temp, $content)

try {
    Set-SCPItem -ComputerName $SshHost -Credential $cred -Path $temp -Destination "~/install-github-runner.sh" -AcceptKey
    $session = New-SSHSession -ComputerName $SshHost -Credential $cred -AcceptKey
    $result = Invoke-SSHCommand -SessionId $session.SessionId -Command "chmod +x ~/install-github-runner.sh && RUNNER_TOKEN='$token' bash ~/install-github-runner.sh" -TimeOut 600
    Write-Host $result.Output
    if ($result.ExitStatus -ne 0) {
        Write-Error $result.Error
        exit $result.ExitStatus
    }
    Remove-SSHSession -SessionId $session.SessionId | Out-Null
}
finally {
    Remove-Item $temp -Force -ErrorAction SilentlyContinue
}

Write-Host "Done. Set ENABLE_DEPLOY=true and DEPLOY_RUNNER=self-hosted in GitHub repo variables."
