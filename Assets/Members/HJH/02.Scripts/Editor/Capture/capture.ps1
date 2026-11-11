# Capture from the command line. Prints "OK <png path>" / "ERR <reason>" lines.
#   Editor open   -> drops Captures/_request.txt (the running editor picks it up within ~1s)
#   Editor closed -> launches Unity in batchmode (-executeMethod)
# Usage: powershell -File capture.ps1 [-Config <json>] [-TimeoutSec 180]
param(
    [string]$Config = "Assets/Members/HJH/02.Scripts/Editor/Capture/Configs/bridge_player.json",
    [string]$Menu = "",          # run one HJH/ menu item in the open editor
    [switch]$Refresh,            # make the open editor import/compile changed scripts
    [int]$TimeoutSec = 180,
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe"
)
$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\..\..")).Path
$dir = Join-Path $root "Captures"
$request = Join-Path $dir "_request.txt"
$result = Join-Path $dir "_result.txt"
New-Item -ItemType Directory -Force $dir | Out-Null
if ($Refresh) { $Config = "refresh" } elseif ($Menu) { $Config = "menu:" + $Menu }
elseif (-not [IO.Path]::IsPathRooted($Config)) { $Config = Join-Path $root $Config }

function Test-EditorOpen {
    $lock = Join-Path $root "Temp\UnityLockfile"
    if (-not (Test-Path $lock)) { return $false }
    try { $fs = [IO.File]::Open($lock, 'Open', 'ReadWrite', 'None'); $fs.Close(); return $false } catch { return $true }
}

$started = Get-Date
$proc = $null
if (Test-EditorOpen) {
    Write-Host "[capture] editor is open -> signal file"
    [IO.File]::WriteAllText($request, $Config)
} else {
    Write-Host "[capture] editor is closed -> batchmode"
    $unityArgs = @("-batchmode", "-projectPath", "`"$root`"", "-executeMethod", "HJH.CaptureTool.CaptureMenu.RunFromCommandLine",
        "-captureConfig", "`"$Config`"", "-logFile", "`"$(Join-Path $dir '_batch.log')`"")
    $proc = Start-Process -FilePath $Unity -ArgumentList $unityArgs -PassThru
}

$deadline = $started.AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if ((Test-Path $result) -and (Get-Item $result).LastWriteTime -ge $started) { Get-Content $result -Encoding UTF8; exit 0 }
    if ($proc -and $proc.HasExited) { Write-Host "ERR Unity exited without a result (code $($proc.ExitCode)). See Captures/_batch.log"; exit 1 }
    Start-Sleep -Milliseconds 500
}
if ($proc -and -not $proc.HasExited) { $proc.Kill() }
if (Test-Path $request) {
    Remove-Item $request -Confirm:$false   # do not let a stale request fire later
    Write-Host "ERR timeout: the editor did not pick up the request (scripts not compiled yet, or the editor is busy)"
}
else { Write-Host "ERR timeout: the request was picked up but capture did not finish" }
exit 1
