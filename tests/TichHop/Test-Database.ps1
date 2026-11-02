#Requires -Version 7.0
Import-Module (Join-Path $PSScriptRoot '../DungChung/TestSupport.psm1') -Force
$root=Get-TestProjectRoot -StartPath $PSScriptRoot
Push-Location $root
try {
    # Dùng bộ kiểm tra rollback có sẵn trong backend; không nhân đôi quy tắc SQL ở nhiều nơi.
    & dotnet (Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll') --test-db
    if ($LASTEXITCODE -ne 0) { throw 'Kiểm tra ràng buộc database thất bại.' }
} finally { Pop-Location }
