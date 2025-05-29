@echo off
cd /d "%~dp0"

start "" "server\server\Chasser.Server.exe"
timeout /t 2 >nul
start "" "client\client\Chasser.exe"
exit
