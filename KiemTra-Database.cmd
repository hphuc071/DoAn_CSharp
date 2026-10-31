@echo off
cd /d "%~dp0"
dotnet run --project src\FourChang.Api -- --check-db
if errorlevel 1 goto failed
dotnet run --no-build --project src\FourChang.Api -- --test-db
:failed
pause
