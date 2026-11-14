$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
$settings = & (Join-Path $PSScriptRoot 'Get-LocalRunSettings.ps1') -Root $root

# Chỉ đưa các cờ giao diện/mô phỏng đã cho phép vào process Web; không nạp mật khẩu SQL.
$localFile = Join-Path $root '.env'
if (Test-Path -LiteralPath $localFile) {
    foreach ($line in Get-Content -LiteralPath $localFile) {
        if ($line -match '^\s*(ASPNETCORE_ENVIRONMENT|Payments__SimulationEnabled|Checkout__OrderCreationEnabled|Orders__FulfillmentSimulationEnabled)\s*=(.*)$') {
            $name = $matches[1]
            if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) {
                [Environment]::SetEnvironmentVariable($name, $matches[2].Trim().Trim('"', "'"))
            }
        }
    }
}

# Kiểm tra đúng Web của repo này; không dừng hoặc nhận nhầm ứng dụng khác đang chiếm cổng.
function Test-ExistingLocalWeb {
    $port = ([Uri]$settings.WebUrl).Port
    $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    if ($listeners.Count -eq 0) { return $false }
    $projectPrefix = (Join-Path $root 'src\Presentation\FourChang.Web').TrimEnd('\') + '\'
    foreach ($ownerId in @($listeners.OwningProcess | Select-Object -Unique)) {
        $owner = Get-CimInstance Win32_Process -Filter "ProcessId=$ownerId" -ErrorAction SilentlyContinue
        $sameProject = $null -ne $owner -and (
            ($owner.ExecutablePath -and $owner.ExecutablePath.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) -or
            ($owner.CommandLine -and $owner.CommandLine.IndexOf($projectPrefix, [StringComparison]::OrdinalIgnoreCase) -ge 0))
        if (-not $sameProject) {
            throw "Cong $port dang duoc ung dung khac su dung (PID $ownerId). Khong tu dong dung tien trinh nay."
        }
    }
    # Có process chưa đủ: chờ trang đăng nhập trả về nội dung thương hiệu trước khi báo sẵn sàng.
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri ($settings.WebUrl + '/Account/Login') -TimeoutSec 3
        return $response.StatusCode -eq 200 -and $response.Content.Contains('4CHANG')
    }
    catch { return $false }
}

# Chặn hai lần bấm Chay-Web cùng lúc, kể cả lúc dotnet đang build và chưa mở cổng.
$port = ([Uri]$settings.WebUrl).Port
$launchLock = [System.Threading.Mutex]::new($false, "Local\FourChang.Web.$port")
$ownsLaunchLock = $false
try {
    if (Test-ExistingLocalWeb) {
        Write-Host ('Web da san sang: ' + $settings.WebUrl + ' (khong khoi dong them)')
        return
    }
    try { $ownsLaunchLock = $launchLock.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $ownsLaunchLock = $true }
    if (-not $ownsLaunchLock -or @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue).Count -gt 0) {
        for ($attempt = 0; $attempt -lt 15; $attempt++) {
            if (Test-ExistingLocalWeb) {
                Write-Host ('Web da san sang: ' + $settings.WebUrl + ' (khong khoi dong them)')
                return
            }
            Start-Sleep -Seconds 1
        }
        throw ('Web dang khoi dong nhung chua san sang: ' + $settings.WebUrl + '. Kiem tra cua so chay Web/log truoc khi thu lai.')
    }
    # Truyền URL API cho đúng process Web; không chuyển bí mật SQL tới giao diện.
    $env:FOURCHANG_API_URL = $settings.ApiUrl
    Write-Host ('Web: ' + $settings.WebUrl)
    & dotnet run --no-launch-profile --project (Join-Path $root 'src\Presentation\FourChang.Web') --urls $settings.WebUrl
    $webExitCode = $LASTEXITCODE
}
finally {
    if ($ownsLaunchLock) { $launchLock.ReleaseMutex() }
    $launchLock.Dispose()
}
exit $webExitCode
