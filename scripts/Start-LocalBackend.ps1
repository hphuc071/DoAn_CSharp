param([string]$ApiUrl = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root

# Cùng biến cấu hình với Web/POS/Admin; chỉ đọc URL, không đưa bí mật .env vào log.
if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = $env:FOURCHANG_API_URL }
if ([string]::IsNullOrWhiteSpace($ApiUrl)) {
    $envFile = Join-Path $root '.env'
    if (Test-Path -LiteralPath $envFile) {
        foreach ($line in [IO.File]::ReadLines($envFile)) {
            if ($line -match '^\s*FOURCHANG_API_URL\s*=(.*)$') { $ApiUrl = $Matches[1].Trim().Trim('"', "'") }
        }
    }
}
if ([string]::IsNullOrWhiteSpace($ApiUrl)) { $ApiUrl = 'http://localhost:15181' }
$endpoint = $null
if (-not [Uri]::TryCreate($ApiUrl, [UriKind]::Absolute, [ref]$endpoint) -or
    $endpoint.Scheme -ne 'http' -or -not $endpoint.IsLoopback -or $endpoint.AbsolutePath -ne '/') {
    throw 'FOURCHANG_API_URL cho launcher local phai la http://localhost:cong hoac http://127.0.0.1:cong.'
}
$healthUrl = $ApiUrl.TrimEnd('/') + '/health/ready'

# Dùng lại backend đang sẵn sàng; không tạo process thứ hai hoặc khởi tạo lại database.
function Test-BackendReady {
    try {
        $ready = Invoke-RestMethod $healthUrl -TimeoutSec 2
        return ($ready.status -eq 'ready' -and $ready.tables -eq 37)
    } catch { return $false }
}
if (Test-BackendReady) { Write-Host "Backend san sang: $ApiUrl"; exit 0 }

# Phát hiện cổng cấm/đang bị chiếm trước khi chạy API; đây không phải lỗi Docker/database.
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $endpoint.Port)
try { $listener.Start() }
catch {
    throw ("Khong mo duoc cong API {0}. Cong dang bi chiem hoac Windows danh rieng (socket 10013). Kiem tra FOURCHANG_API_URL; cong mac dinh hien tai la 15181. Khong can tao lai database." -f $endpoint.Port)
}
finally { $listener.Stop() }

$dll = Join-Path $root 'src\Backend\FourChang.Api\bin\Debug\net8.0\FourChang.Api.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    dotnet build (Join-Path $root 'src\Backend\FourChang.Api\FourChang.Api.csproj') -m:1 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build backend that bai.' }
}
$logs = Join-Path $root 'artifacts\run'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
$server = Start-Process -FilePath 'dotnet' -ArgumentList @('"'+$dll+'"', '--urls', $ApiUrl) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'api.stdout.log') -RedirectStandardError (Join-Path $logs 'api.stderr.log')
for ($i = 0; $i -lt 20; $i++) {
    Start-Sleep -Seconds 1
    if (Test-BackendReady) { Write-Host "Backend san sang: $ApiUrl"; exit 0 }
    if ($server.HasExited) {
        throw 'API da dung khi khoi dong. Xem artifacts/run/api.stdout.log va api.stderr.log de phan biet loi cong, cau hinh va database.'
    }
}
throw 'API da mo nhung database chua dat health/ready. Xem artifacts/run/api.stdout.log va api.stderr.log; kiem tra SQL Docker va schema. Khong tu xoa database.'
