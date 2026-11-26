param(
    [ValidateSet("Install", "Remove", "Status")]
    [string]$Action = "Status",
    [string]$TaskName = "AutoDev_3DCivCiv_NightShift",
    [string]$Time = "02:30",
    [int]$MaxTasks = 3
)

$ErrorActionPreference = "Continue"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$RunnerScript = Join-Path $RepoRoot "auto_dev\scripts\run_night.ps1"

switch ($Action) {
    "Install" {
        $Cmd = "powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$RunnerScript`" -MaxTasks $MaxTasks"
        schtasks.exe /Create /TN $TaskName /SC DAILY /ST $Time /TR $Cmd /F
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Zamanlanmis gorev kuruldu: '$TaskName' (Her gun $Time, max $MaxTasks gorev)"
        } else {
            Write-Host "[HATA] Zamanlanmis gorev kurulamadi (kod $LASTEXITCODE)."
            exit $LASTEXITCODE
        }
    }
    "Remove" {
        schtasks.exe /Delete /TN $TaskName /F 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Zamanlanmis gorev kaldirildi: '$TaskName'"
        } else {
            Write-Host "[BILGI] '$TaskName' adinda gorev bulunamadi veya silinemedi."
        }
    }
    "Status" {
        $Out = schtasks.exe /Query /TN $TaskName /FO LIST 2>&1
        if ($LASTEXITCODE -eq 0) {
            $Out | Write-Output
        } else {
            Write-Host "[BILGI] '$TaskName' kurulu degil."
            Write-Host "Kurmak icin: powershell -ExecutionPolicy Bypass -File auto_dev\scripts\setup_scheduler.ps1 -Action Install -Time 02:30 -MaxTasks 3"
        }
    }
}
