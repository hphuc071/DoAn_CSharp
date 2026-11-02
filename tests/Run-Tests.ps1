#Requires -Version 7.0
param([ValidateSet('TatCa','KhachHang','NhanVien','QuanLy')][string]$Actor='TatCa',
    [string]$UseCase,[int]$Port=5183,[switch]$IncludePresentation,[switch]$IncludeRegression,[switch]$KeepDatabase)
$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'test-manifest.json') -Raw | ConvertFrom-Json
if($UseCase -and $UseCase -notin $manifest.useCases.code){throw 'Mã UC không thuộc 26 UC đã chốt.'}
$selected=@($manifest.useCases | Where-Object {($Actor -eq 'TatCa' -or $_.actor -eq $Actor) -and (-not $UseCase -or $_.code -eq $UseCase)})
if(-not $selected.Count){throw 'UC không thuộc actor đã chọn.'}
$ready=@($selected | Where-Object {$_.script})
$pending=@($selected | Where-Object {-not $_.script})
foreach($entry in $pending){Write-Output ('CHƯA CÓ TEST TỰ ĐỘNG: '+$entry.code+' - '+$entry.name)}
foreach($entry in $ready){
    Write-Output ('ĐANG CHẠY: '+$entry.code)
    & (Join-Path $PSScriptRoot $entry.script) -Port $Port -KeepDatabase:$KeepDatabase -IncludePresentation:$IncludePresentation -IncludeRegression:$IncludeRegression
    if(-not $?){throw ('Test thất bại: '+$entry.code)}
}
if(-not $ready.Count){Write-Output 'Không có bộ test đã triển khai trong phạm vi được chọn; không đánh dấu PASS.'}
