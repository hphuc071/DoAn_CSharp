@echo off
cd /d "%~dp0"
dotnet run --no-launch-profile --project src\FourChang.Api --urls http://localhost:5181
if errorlevel 1 pause
