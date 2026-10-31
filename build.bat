@echo off
setlocal

set "SAMELLA_POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
where pwsh.exe >nul 2>&1
if not errorlevel 1 set "SAMELLA_POWERSHELL=pwsh.exe"

"%SAMELLA_POWERSHELL%" -NoLogo -NoProfile -File "%~dp0build.ps1" %*
exit /b %errorlevel%
