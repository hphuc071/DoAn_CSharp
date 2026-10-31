@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-LocalBackend.ps1"
if errorlevel 1 goto backendfailed
echo Web: http://localhost:5180
dotnet run --project "%~dp0src\FourChang.Web" --urls http://localhost:5180
if errorlevel 1 pause

exit /b %errorlevel%

:backendfailed
echo Backend chua san sang. Chay ChuanBi-Database.ps1 truoc.
pause
exit /b 1
