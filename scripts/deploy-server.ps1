<# .SYNOPSIS
一键发布 + 部署 HMC Server 到远程 Linux 服务器

.PARAMETER ServerHost
目标服务器 IP 或主机名 (默认: 192.168.31.2)

.PARAMETER ServerUser
SSH 用户名 (默认: root)

.PARAMETER ServerPassword
SSH 密码 (不提供则使用 SSH Key 或交互输入)

.PARAMETER RemotePath
服务器上的部署路径 (默认: /opt/hmc)

.PARAMETER ServicePort
Server 监听端口 (默认: 5000)

.EXAMPLE
.\scripts\deploy-server.ps1
    发布并部署到 192.168.31.2，交互式输入密码

.EXAMPLE
.\scripts\deploy-server.ps1 -ServerHost 10.0.0.5 -ServerUser admin
    发布并部署到指定服务器

.NOTES
首次使用建议配置 SSH Key 免密登录:
  ssh-copy-id root@192.168.31.2
#>

param(
    [string]$ServerHost = "192.168.31.2",
    [string]$ServerUser = "root",
    [string]$ServerPassword,
    [string]$RemotePath = "/opt/hmc",
    [int]$ServicePort = 5000
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."
$publishDir = "$root\publish\linux-x64"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

Write-Host @"

╔══════════════════════════════════════════╗
║     HMC Server Deploy                  ║
║     $($ServerHost.PadRight(30)) ║
╚══════════════════════════════════════════╝

"@ -ForegroundColor Cyan

# ── 1. Publish ──
Write-Host "[1/4] Publishing Server (linux-x64, self-contained)..." -ForegroundColor Yellow
dotnet publish "$root\src\HMC.Server\HMC.Server.csproj" `
    -c Release `
    -r linux-x64 `
    --self-contained true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
Write-Host "  -> Published to $publishDir" -ForegroundColor Green

# ── 2. Create server config ──
Write-Host "[2/4] Creating remote config..." -ForegroundColor Yellow
$serverJson = @"
{
  "Database": { "Path": "$RemotePath/data/hmc.db" },
  "AllowedHosts": "*",
  "Serilog": {
    "MinimumLevel": { "Default": "Information", "Override": { "Microsoft": "Warning" } }
  }
}
"@
$serverJson | Set-Content "$publishDir\appsettings.json"
$serverJson | Set-Content "$publishDir\appsettings.Production.json"

# Create a systemd service file
$serviceFile = @"
[Unit]
Description=HMC Monitoring Server
After=network.target

[Service]
Type=simple
WorkingDirectory=$RemotePath
ExecStart=$RemotePath/HMC.Server --urls http://0.0.0.0:$ServicePort
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=HMC_SERVER_IP=$ServerHost

[Install]
WantedBy=multi-user.target
"@
$serviceFile | Set-Content "$publishDir\hmc-server.service"

# ── 3. Build SSH args ──
$sshArgs = @()
$scpArgs = @()
$sshTarget = "${ServerUser}@${ServerHost}"

if ($ServerPassword) {
    # Use sshpass if available
    $env:SSHPASS = $ServerPassword
    $sshPrefix = "sshpass -e"
    $scpPrefix = "sshpass -e"
    Write-Host "  -> Using password auth" -ForegroundColor Yellow
} else {
    $sshPrefix = ""
    $scpPrefix = ""
    Write-Host "  -> Using SSH key or interactive auth" -ForegroundColor Yellow
}

# ── 4. Deploy ──
Write-Host "[3/4] Deploying to $ServerHost..." -ForegroundColor Yellow

# Stop existing service
Write-Host "  -> Stopping remote service..."
if ($sshPrefix) {
    $stopCmd = "$sshPrefix ssh -o StrictHostKeyChecking=no $sshTarget 'systemctl stop hmc-server 2>/dev/null; pkill -f HMC.Server 2>/dev/null; echo done'"
    Invoke-Expression $stopCmd
} else {
    ssh -o StrictHostKeyChecking=no $sshTarget "systemctl stop hmc-server 2>/dev/null; pkill -f HMC.Server 2>/dev/null; echo done"
}

# Create remote dir
Write-Host "  -> Creating remote directory: $RemotePath"
if ($sshPrefix) {
    Invoke-Expression "$sshPrefix ssh -o StrictHostKeyChecking=no $sshTarget 'mkdir -p $RemotePath/data $RemotePath/logs'"
} else {
    ssh -o StrictHostKeyChecking=no $sshTarget "mkdir -p $RemotePath/data $RemotePath/logs"
}

# SCP files
Write-Host "  -> Uploading files..."
if ($scpPrefix) {
    Invoke-Expression "$scpPrefix scp -o StrictHostKeyChecking=no -r '$publishDir/*' ${sshTarget}:${RemotePath}/"
} else {
    scp -o StrictHostKeyChecking=no -r "$publishDir\*" "${sshTarget}:${RemotePath}/"
}

# Set permissions
Write-Host "  -> Setting permissions..."
if ($sshPrefix) {
    Invoke-Expression "$sshPrefix ssh -o StrictHostKeyChecking=no $sshTarget 'chmod +x $RemotePath/HMC.Server'"
} else {
    ssh -o StrictHostKeyChecking=no $sshTarget "chmod +x $RemotePath/HMC.Server"
}

# Install systemd service (first time only)
Write-Host "  -> Installing systemd service..."
if ($sshPrefix) {
    Invoke-Expression "$sshPrefix ssh -o StrictHostKeyChecking=no $sshTarget 'cp $RemotePath/hmc-server.service /etc/systemd/system/ && systemctl daemon-reload && systemctl enable hmc-server && systemctl start hmc-server'"
} else {
    ssh -o StrictHostKeyChecking=no $sshTarget "cp $RemotePath/hmc-server.service /etc/systemd/system/ && systemctl daemon-reload && systemctl enable hmc-server && systemctl start hmc-server"
}

# ── 5. Verify ──
Write-Host "[4/4] Verifying deployment..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

if ($sshPrefix) {
    $status = Invoke-Expression "$sshPrefix ssh -o StrictHostKeyChecking=no $sshTarget 'systemctl status hmc-server --no-pager'"
} else {
    $status = ssh -o StrictHostKeyChecking=no $sshTarget "systemctl status hmc-server --no-pager"
}

Write-Host $status

Write-Host @"

=== Deploy Complete ===
  Server:  http://${ServerHost}:${ServicePort}
  Health:  http://${ServerHost}:${ServicePort}/health
  Logs:    ssh ${sshTarget} 'journalctl -u hmc-server -f'

Next steps:
  1. Update Agent appsettings.json:
     "ServerUrl": "http://${ServerHost}:${ServicePort}"
  2. Open http://${ServerHost}:${ServicePort}/health in browser
"@ -ForegroundColor Green
