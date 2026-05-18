# ITCareHelpdesk — setup automat al bazei de date
# Ruleaza toate scripturile in ordine pe instanta default localhost\SQLEXPRESS.
# Comanda: powershell -ExecutionPolicy Bypass -File .\setup-db.ps1

param(
    [string]$Server = "localhost\SQLEXPRESS",
    [switch]$Reset
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "ITCare Helpdesk — DB Setup" -ForegroundColor Cyan
Write-Host "===========================" -ForegroundColor Cyan
Write-Host "Server: $Server"
Write-Host ""

# Verificam ca sqlcmd este instalat
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Host "EROARE: sqlcmd nu este instalat. Instaleaza SQL Server Command Line Tools." -ForegroundColor Red
    Write-Host "https://learn.microsoft.com/sql/tools/sqlcmd/sqlcmd-utility"
    exit 1
}

# Test conexiune
Write-Host "Testez conexiunea..." -NoNewline
try {
    & sqlcmd -S $Server -E -Q "SELECT @@VERSION" -h -1 -W | Out-Null
    Write-Host " OK" -ForegroundColor Green
} catch {
    Write-Host " ESUAT" -ForegroundColor Red
    Write-Host "Nu pot conecta la $Server. Verifica ca serviciul SQL Server ruleaza."
    exit 1
}

# Drop si recreate daca s-a cerut reset complet
if ($Reset) {
    Write-Host "Reset complet: drop ITCareHelpdesk..." -ForegroundColor Yellow
    & sqlcmd -S $Server -E -Q "IF DB_ID('ITCareHelpdesk') IS NOT NULL BEGIN ALTER DATABASE ITCareHelpdesk SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ITCareHelpdesk; END"
}

# Ruleaza scripturile in ordine
$scripts = @("01_main.sql", "02_extensions.sql", "03_seed.sql")
foreach ($s in $scripts) {
    $path = Join-Path $here $s
    if (-not (Test-Path $path)) {
        Write-Host "EROARE: lipseste $path" -ForegroundColor Red
        exit 1
    }
    Write-Host "Ruleaza $s..." -ForegroundColor Cyan
    & sqlcmd -S $Server -E -i $path
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Script-ul $s a esuat cu cod $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host "  DB-ul este gata. Poti porni aplicatia." -ForegroundColor Green
Write-Host "  Login: admin / admin123" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
