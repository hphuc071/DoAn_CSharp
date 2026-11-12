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

# Truyền URL API cho đúng process Web; không chuyển bí mật SQL tới giao diện.
$env:FOURCHANG_API_URL = $settings.ApiUrl
Write-Host ('Web: ' + $settings.WebUrl)
& dotnet run --no-launch-profile --project (Join-Path $root 'src\Presentation\FourChang.Web') --urls $settings.WebUrl
exit $LASTEXITCODE
