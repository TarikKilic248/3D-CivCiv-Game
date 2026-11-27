param(
    [int]$MaxTasks = 3
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $RepoRoot

$LogDir = Join-Path $RepoRoot ".autodev\logs"
if (-not (Test-Path $LogDir)) {
    New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
}

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $LogDir "night_$Stamp.log"

$PythonCmd = Get-Command python -ErrorAction SilentlyContinue
if (-not $PythonCmd) {
    "Python bulunamadi!" | Out-File -FilePath $LogFile -Encoding utf8
    exit 1
}

"=== AutoDev Gece Vardiyasi Basladi: $(Get-Date -Format o) ===" | Out-File -FilePath $LogFile -Encoding utf8
& $PythonCmd.Source "auto_dev\cli.py" run --max-tasks $MaxTasks *>> $LogFile
$Code = $LASTEXITCODE
"=== AutoDev Bitti (Kod: $Code): $(Get-Date -Format o) ===" | Out-File -FilePath $LogFile -Append -Encoding utf8

# Keep latest 30 logs
Get-ChildItem -Path $LogDir -Filter "night_*.log" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -Skip 30 |
    Remove-Item -Force -ErrorAction SilentlyContinue

exit $Code
