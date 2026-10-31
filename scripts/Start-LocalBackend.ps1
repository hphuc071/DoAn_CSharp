$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
try { $ready = Invoke-RestMethod 'http://localhost:5181/health/ready' -TimeoutSec 4; if($ready.status -eq 'ready' -and $ready.tables -eq 34) { exit 0 } } catch { }
$dll = Join-Path $root 'src\FourChang.Api\bin\Debug\net8.0\FourChang.Api.dll'
if(-not (Test-Path -LiteralPath $dll)) {
    dotnet build (Join-Path $root 'src\FourChang.Api\FourChang.Api.csproj') -m:1 --nologo
    if($LASTEXITCODE -ne 0) { throw 'Build backend that bai.' }
}
$logs = Join-Path $root 'artifacts\run'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Start-Process -FilePath 'dotnet' -ArgumentList @('"'+$dll+'"','--urls','http://localhost:5181') -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'api.stdout.log') -RedirectStandardError (Join-Path $logs 'api.stderr.log') | Out-Null
for($i=0;$i -lt 20;$i++) {
    Start-Sleep -Seconds 1
    try { $ready = Invoke-RestMethod 'http://localhost:5181/health/ready' -TimeoutSec 3; if($ready.status -eq 'ready' -and $ready.tables -eq 34) { exit 0 } } catch { }
}
throw 'Backend chua san sang. Mo Docker Desktop va chay ChuanBi-Database.ps1 truoc. Xem artifacts/run/api.stderr.log.'
