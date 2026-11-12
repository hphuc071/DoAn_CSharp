param([string]$Root = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'

# Chỉ đọc URL cần chạy, không nạp/in các mật khẩu trong .env.
$values = @{}
$file = Join-Path $Root '.env'
if (Test-Path -LiteralPath $file) {
    foreach ($line in Get-Content -LiteralPath $file) {
        if ($line -match '^\s*(FOURCHANG_API_URL|FOURCHANG_WEB_URL)\s*=(.*)$') {
            $values[$matches[1]] = $matches[2].Trim().Trim('"', "'")
        }
    }
}

# URL môi trường được ưu tiên như ApiClient; chỉ cho phép địa chỉ local của bộ lệnh chạy local.
function Resolve-LocalUrl([string]$Name, [string]$Default) {
    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) { $value = $values[$Name] }
    if ([string]::IsNullOrWhiteSpace($value)) { $value = $Default }
    $uri = $null
    if (-not [Uri]::TryCreate($value, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -notin @('http', 'https') -or -not $uri.IsLoopback -or
        $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $uri.AbsolutePath -ne '/') {
        throw "Cau hinh $Name phai la URL localhost hop le, khong kem mat khau, query hoac duong dan."
    }
    return $uri.GetLeftPart([UriPartial]::Authority)
}

[pscustomobject]@{
    ApiUrl = Resolve-LocalUrl 'FOURCHANG_API_URL' 'http://localhost:15881'
    WebUrl = Resolve-LocalUrl 'FOURCHANG_WEB_URL' 'http://localhost:15880'
}
