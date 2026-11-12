param([switch]$Foreground)
$ErrorActionPreference = 'Stop'
# Schema ADM-01..05 có 37 entity; không nhận nhầm API cũ chỉ sẵn sàng với 35 bảng.
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
# Backend, Web và Desktop dùng cùng URL local đã chốt, tránh cổng bị Windows dành riêng.
$settings = & (Join-Path $PSScriptRoot 'Get-LocalRunSettings.ps1') -Root $root
$healthUrl = $settings.ApiUrl + '/health/ready'
# Khong mo them process khi API dang chay, ke ca goi Chay-Backend.cmd lan nua.
try { $ready = Invoke-RestMethod $healthUrl -TimeoutSec 4; if($ready.status -eq 'ready' -and $ready.tables -eq 37) { Write-Host ('Backend da san sang: ' + $settings.ApiUrl); exit 0 } } catch { }
if ($Foreground) {
    & dotnet run --no-launch-profile --project (Join-Path $root 'src\Backend\FourChang.Api') --urls $settings.ApiUrl
    exit $LASTEXITCODE
}
$dll = Join-Path $root 'src\Backend\FourChang.Api\bin\Debug\net8.0\FourChang.Api.dll'
if(-not (Test-Path -LiteralPath $dll)) {
    dotnet build (Join-Path $root 'src\Backend\FourChang.Api\FourChang.Api.csproj') -m:1 --nologo
    if($LASTEXITCODE -ne 0) { throw 'Build backend that bai.' }
}
$logs = Join-Path $root 'artifacts\run'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Start-Process -FilePath 'dotnet' -ArgumentList @('"'+$dll+'"','--urls',$settings.ApiUrl) -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'api.stdout.log') -RedirectStandardError (Join-Path $logs 'api.stderr.log') | Out-Null
for($i=0;$i -lt 20;$i++) {
    Start-Sleep -Seconds 1
    try { $ready = Invoke-RestMethod $healthUrl -TimeoutSec 3; if($ready.status -eq 'ready' -and $ready.tables -eq 37) { exit 0 } } catch { }
}
throw ("Backend chua san sang tai " + $settings.ApiUrl + ". Kiem tra Docker/database, cong local va artifacts/run/api.stderr.log.")
