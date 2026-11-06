@echo off
cd /d "%~dp0"
dotnet run --no-launch-profile --project src\Backend\FourChang.Api --urls http://localhost:15181
if errorlevel 1 pause
