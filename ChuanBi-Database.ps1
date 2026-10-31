$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
function New-LocalSecret {
    $bytes = New-Object byte[] 24
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes); $rng.Dispose()
    return 'Fc!' + [Convert]::ToBase64String($bytes) + '9a'
}
$envPath = Join-Path $PSScriptRoot '.env'

$lines = New-Object 'System.Collections.Generic.List[string]'
if(Test-Path -LiteralPath $envPath) { foreach($line in [IO.File]::ReadAllLines($envPath)) { $lines.Add($line) } }
foreach($key in @('SQL_SA_PASSWORD','SQL_APP_PASSWORD','ADMIN_INITIAL_PASSWORD','FOURCHANG_POS_KEY')) {
    $found = -1
    for($i=0;$i -lt $lines.Count;$i++) {
        if($lines[$i].StartsWith($key+'=',[StringComparison]::Ordinal)) {
            if($found -ge 0) { throw ('Bien cau hinh bi lap: '+$key) }
            $found=$i
        }
    }
    if($found -lt 0) { $lines.Add($key+'='+(New-LocalSecret)); continue }
    $current=$lines[$found].Substring($key.Length+1).Trim()
    if([string]::IsNullOrWhiteSpace($current) -or $current -match '^(THAY_|DIEN_|CHANGE_|REPLACE_|<)') { $lines[$found]=$key+'='+(New-LocalSecret) }
}
foreach($pair in @('FOURCHANG_LOCAL=true','FOURCHANG_DATABASE=FourChangDb','FOURCHANG_SQL_SERVER=127.0.0.1,14330','FOURCHANG_API_URL=http://localhost:5181')) {
    $prefix=$pair.Split('=')[0]+'='
    if(-not ($lines | Where-Object { $_.StartsWith($prefix,[StringComparison]::Ordinal) })) { $lines.Add($pair) }
}
[IO.File]::WriteAllLines($envPath,$lines,(New-Object Text.UTF8Encoding($false)))
$lines=$null

docker info --format '{{.OSType}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Hay mo Docker Desktop, cho Engine running, roi chay lai.' }
docker compose up -d
if ($LASTEXITCODE -ne 0) { throw 'Khong khoi dong duoc SQL Server.' }
$ready = $false
for($i=0;$i -lt 90;$i++) {
    $health = docker inspect fourchang-sqlserver-1 --format '{{.State.Health.Status}}' 2>$null
    if($health -eq 'healthy') { $ready = $true; break }
    Start-Sleep -Seconds 1
}
if(-not $ready) { throw 'SQL Server chua san sang. Xem docker compose logs sqlserver.' }
dotnet build FourChang.sln -m:1 --nologo
if($LASTEXITCODE -ne 0) { throw 'Build that bai.' }
dotnet run --no-build --project src/FourChang.Api -- --init-db
if($LASTEXITCODE -ne 0) { throw 'Khoi tao that bai; khong tu xoa database hoac volume. Xem thong bao o tren.' }
Write-Host 'SAN SANG. Chay Chay-Backend.cmd, roi Web / NhanVien / QuanLy.'
Write-Host 'Quan ly: quanly. Mat khau ban dau nam trong ADMIN_INITIAL_PASSWORD cua file .env.'
