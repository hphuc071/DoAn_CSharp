#Requires -Version 7.0
# Tiện ích hạ tầng dùng chung cho mọi actor. Không chứa quy tắc riêng của một UC.
function Get-TestProjectRoot {
    param([Parameter(Mandatory)][string]$StartPath)
    $directory=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($StartPath))
    while ($directory -and -not (Test-Path -LiteralPath (Join-Path $directory.FullName 'FourChang.sln'))) { $directory=$directory.Parent }
    if (-not $directory) { throw 'Không tìm thấy FourChang.sln từ thư mục test.' }
    $directory.FullName
}
function Get-TestLocalSettings {
    param([Parameter(Mandatory)][string]$ProjectRoot)
    $file=Join-Path $ProjectRoot '.env'
    if (-not (Test-Path -LiteralPath $file)) { throw 'Chưa có .env. Chạy ChuanBi-Database.ps1 trước.' }
    $settings=@{}
    foreach ($line in [IO.File]::ReadLines($file)) {
        if ($line -match '^([A-Z_][A-Z0-9_]*)=(.*)$') { $settings[$Matches[1]]=$Matches[2].Trim().Trim('"',"'") }
    }
    # Chỉ trả về cho script gọi; tuyệt đối không Write-Host/ghi settings vào kết quả test.
    return $settings
}
function Get-TestArtifactsDirectory {
    param([string]$ProjectRoot,[string]$Actor,[string]$UseCase,[string]$RunId)
    foreach ($part in @($Actor,$UseCase,$RunId)) { if ($part -notmatch '^[A-Za-z0-9_-]+$') { throw 'Tên thư mục kết quả test không hợp lệ.' } }
    $path=Join-Path $ProjectRoot "artifacts/tests/$Actor/$UseCase/$RunId"
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    return $path
}
function Invoke-TestApiRequest {
    param([string]$BaseUrl,[string]$Method,[string]$Path,$Body=$null,[hashtable]$Headers=@{})
    $arguments=@{Method=$Method;Uri=$BaseUrl.TrimEnd('/')+$Path;Headers=$Headers;ContentType='application/json';SkipHttpErrorCheck=$true;TimeoutSec=30}
    if ($null -ne $Body) { $arguments.Body=ConvertTo-Json $Body -Depth 12 -Compress }
    Invoke-WebRequest @arguments
}
function Read-TestJson { param($Response) $Response.Content | ConvertFrom-Json }
function Invoke-TestSql {
    param([string]$ContainerId,[string]$Database,[string]$Query)
    # SQLCMDPASSWORD do runner đặt/tự khôi phục trong finally; không đưa mật khẩu vào dòng lệnh.
    $output=& docker exec --env SQLCMDPASSWORD $ContainerId /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -I -b -d $Database -h -1 -W -w 65535 -Q ("SET NOCOUNT ON; "+$Query) 2>&1
    if ($LASTEXITCODE -ne 0) { throw ('SQL kiểm thử thất bại: '+($output -join "`n")) }
    ($output | Where-Object { "$($_)".Trim().Length -gt 0 }) -join "`n"
}
Export-ModuleMember -Function Get-TestProjectRoot,Get-TestLocalSettings,Get-TestArtifactsDirectory,Invoke-TestApiRequest,Read-TestJson,Invoke-TestSql
