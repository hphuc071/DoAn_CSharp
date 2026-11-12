@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-LocalBackend.ps1"
if errorlevel 1 goto backendfailed
set "APP=%~dp0src\Presentation\FourChang.Admin\bin\Debug\net8.0-windows\FourChang.Admin.exe"
rem Co SDK thi build lai de giao dien khong bi giu DLL cu sau khi sua source.
where dotnet >nul 2>nul
if errorlevel 1 goto useexisting
 dotnet build "%~dp0src\Presentation\FourChang.Admin\FourChang.Admin.csproj" --nologo
if errorlevel 1 goto failed
:launch
start "" "%APP%"
exit /b 0
:useexisting
if exist "%APP%" goto launch
:missing
echo Khong tim thay .NET SDK. Can cai .NET 8 SDK de build lan dau.
goto failed
:failed
echo Khong mo duoc ung dung. Hay dong cac cua so Quan ly cu roi chay lai. Neu van loi, gui noi dung o tren.
pause
exit /b 1
:backendfailed
echo Backend chua san sang. Chay ChuanBi-Database.ps1 truoc.
pause
exit /b 1
