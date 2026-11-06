@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-LocalBackend.ps1"
if errorlevel 1 goto backendfailed
echo Web: http://localhost:15180
dotnet run --project "%~dp0src\Presentation\FourChang.Web" --urls http://localhost:15180
if errorlevel 1 pause

exit /b %errorlevel%

:backendfailed
echo Backend chua san sang. Xem loi cu the o tren va artifacts\run\api.stdout.log / api.stderr.log.
pause
exit /b 1
