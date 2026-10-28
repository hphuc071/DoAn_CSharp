@echo off
setlocal
cd /d "%~dp0"
set "APP=%~dp0src\MocCafe.Admin\bin\Debug\net8.0-windows\MocCafe.Admin.exe"
if exist "%APP%" goto launch
where dotnet >nul 2>nul
if errorlevel 1 goto missing
 dotnet build "%~dp0src\MocCafe.Admin\MocCafe.Admin.csproj" --nologo
if errorlevel 1 goto failed
:launch
start "" "%APP%"
exit /b 0
:missing
echo Khong tim thay .NET SDK. Can cai .NET 8 SDK de build lan dau.
goto failed
:failed
echo Khong mo duoc ung dung. Vui long gui noi dung loi o tren.
pause
exit /b 1