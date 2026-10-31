@echo off
setlocal

call "%~dp0build.bat" -Run -Offline %*
exit /b %errorlevel%
