# 第二轮冒烟截图脚本
# 1) 清理旧截图
# 2) 启动 Avalonia 应用（--smoke 模式，自动注入 PG 连接、打开关键窗口并截屏）
# 3) 进程退出后列出截图

param(
    [string]$ExePath = "d:\dotnet\DatabaseManager\DatabaseManager.Avalonia\DatabaseManager.Avalonia\bin\Debug\net8.0\DatabaseManager.Avalonia.exe",
    [string]$OutputDir = "d:\dotnet\DatabaseManager\DatabaseManager.Avalonia\docs\smoke"
)

$ErrorActionPreference = "Stop"

# 1) 清理
if (Test-Path $OutputDir) {
    Get-ChildItem -Path $OutputDir -Include *.png -File | Remove-Item -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# 2) 杀旧进程
Get-Process -Name "DatabaseManager.Avalonia" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 3) 启动
Write-Host "[smoke] 启动 $ExePath --smoke"
$proc = Start-Process -FilePath $ExePath -ArgumentList "--smoke" -PassThru -WindowStyle Hidden
Write-Host "[smoke] PID = $($proc.Id)"

# 4) 等进程结束（冒烟完成后会自动 Shutdown）
$exited = $proc.WaitForExit(180000)
if (-not $exited) {
    Write-Host "[smoke] 进程未在 180s 内结束，强制终止"
    Stop-Process -Id $proc.Id -Force
}

# 5) 列出截图
$pngs = Get-ChildItem -Path $OutputDir -Filter "*.png" | Sort-Object Name
Write-Host "[smoke] 截图 $($pngs.Count) 张："
$pngs | ForEach-Object { Write-Host ("  {0,-40} {1,8} bytes" -f $_.Name, $_.Length) }
