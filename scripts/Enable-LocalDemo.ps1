$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $root '.env'
if (-not (Test-Path -LiteralPath $envPath)) { throw 'Chay ChuanBi-Database.ps1 truoc de tao cau hinh local.' }
$settings = & (Join-Path $PSScriptRoot 'Get-LocalRunSettings.ps1') -Root $root
$lines = [Collections.Generic.List[string]]::new()
foreach ($line in [IO.File]::ReadAllLines($envPath)) { $lines.Add($line) }
$localFlag = @($lines | Where-Object { $_ -match '^\s*FOURCHANG_LOCAL\s*=\s*["'']?true["'']?\s*$' })
if ($localFlag.Count -ne 1) { throw 'Chi bat mo phong cho cau hinh FOURCHANG_LOCAL=true.' }
# Opt-in tường minh cho đồ án local; không mở xử lý ngân hàng thật hoặc worker đối soát chưa có nguồn ngân hàng.
$values = [ordered]@{
    ASPNETCORE_ENVIRONMENT = 'Development'
    Checkout__OrderCreationEnabled = 'true'
    Orders__CustomerCreationEnabled = 'true'
    Payments__SimulationEnabled = 'true'
    Payments__SimulationPublicBaseUrl = $settings.ApiUrl
    Orders__FulfillmentSimulationEnabled = 'true'
    Orders__PendingPaymentTimeoutMinutes = '15'
    Orders__SqlIntegrationValidated = 'true'
    Orders__CancellationEnabled = 'true'
}
$before = [ordered]@{}
foreach ($key in $values.Keys) {
    $indices = @()
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -match ('^\s*' + [regex]::Escape($key) + '\s*=')) { $indices += $index }
    }
    if ($indices.Count -gt 1) { throw "Cau hinh bi lap: $key" }
    if ($indices.Count -eq 1) {
        $before[$key] = $lines[$indices[0]].Substring($lines[$indices[0]].IndexOf('=') + 1).Trim()
        $lines[$indices[0]] = $key + '=' + $values[$key]
    } else {
        $before[$key] = $null
        $lines.Add($key + '=' + $values[$key])
    }
}
$runDir = Join-Path $root 'artifacts/run'
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$history = Join-Path $runDir ('local-demo-flags-before-' + [Guid]::NewGuid().ToString('N') + '.json')
$before | ConvertTo-Json | Set-Content -LiteralPath $history -Encoding utf8
$temporary = Join-Path $root '.env.local-demo.tmp'
[IO.File]::WriteAllLines($temporary, $lines, [Text.UTF8Encoding]::new($false))
Move-Item -LiteralPath $temporary -Destination $envPath -Force
Write-Host 'Da bat dat hang, huy theo quy trinh duyet, QR mo phong, giao hang va chon ban local.'
Write-Host ('API: ' + $settings.ApiUrl + '; Web: ' + $settings.WebUrl)
Write-Host 'Khoi dong lai API/Web de ap dung. QR nay chi mo phong, khong xac minh ngan hang that.'
