@echo off
cd /d "%~dp0"
echo Web: http://localhost:5180
dotnet run --project "%~dp0src\MocCafe.Web" --urls http://localhost:5180
if errorlevel 1 pause
