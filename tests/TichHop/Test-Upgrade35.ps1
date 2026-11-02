#Requires -Version 7.0
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot '../DungChung/TestSupport.psm1') -Force
$root=Get-TestProjectRoot -StartPath $PSScriptRoot
$settings=Get-TestLocalSettings -ProjectRoot $root
$database='FourChang_Upgrade35_Test_'+[Guid]::NewGuid().ToString('N')
$saved=@{};foreach($key in @('SQLCMDPASSWORD','FOURCHANG_DATABASE','ConnectionStrings__FourChang')){$saved[$key]=[Environment]::GetEnvironmentVariable($key)}
Push-Location $root
try{
    $env:SQLCMDPASSWORD=$settings['SQL_SA_PASSWORD'];$env:FOURCHANG_DATABASE=$database
    Remove-Item Env:ConnectionStrings__FourChang -ErrorAction SilentlyContinue
    $container=(& docker compose ps -q sqlserver | Select-Object -First 1)
    if(-not $container){throw 'Docker SQL Server chưa chạy.'}
    Invoke-TestSql $container master "CREATE DATABASE [$database]" | Out-Null
    # Dựng schema phiên bản cũ thật; không giả bộ migration trên database đã có sẵn bảng mới.
    Get-Content -LiteralPath (Join-Path $root 'docs/database/34-bang/schema.sql') -Raw |
        & docker exec -i --env SQLCMDPASSWORD $container /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -I -b -f 65001 -d $database
    if($LASTEXITCODE -ne 0){throw 'Dựng schema 34 bảng thất bại.'}
    # Giả lập bảng hỗ trợ SSMS với đúng dấu metadata; bootstrap phải giữ nguyên nó.
    Invoke-TestSql $container $database "CREATE TABLE dbo.sysdiagrams(diagram_id int PRIMARY KEY,name sysname); EXEC sys.sp_addextendedproperty @name=N'microsoft_database_tools_support',@value=1,@level0type=N'SCHEMA',@level0name=N'dbo',@level1type=N'TABLE',@level1name=N'sysdiagrams'; INSERT dbo.sysdiagrams VALUES(1,N'ERD đã lưu');" | Out-Null
    & dotnet (Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll') --init-db
    if($LASTEXITCODE -ne 0){throw 'Nâng cấp 34 → 35 thất bại.'}
    & dotnet (Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll') --check-db
    if($LASTEXITCODE -ne 0){throw 'Manifest 35 bảng chưa khớp.'}
    $count=Invoke-TestSql $container $database 'SELECT COUNT(*) FROM dbo.sysdiagrams WHERE diagram_id=1'
    if($count.Trim() -ne '1'){throw 'Không bảo toàn sysdiagrams.'}
    Write-Output 'PASS: Nâng cấp từ 34 bảng thật, bảo toàn sysdiagrams, xác minh đúng 35 bảng nghiệp vụ / 73 FK.'
    & dotnet (Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll') --test-db
    if($LASTEXITCODE -ne 0){throw 'Test ràng buộc database thất bại.'}
}finally{
    # Chỉ xóa chính database tạm của lần chạy, không nhận tên database bên ngoài.
    if($container -and $database -cmatch '^FourChang_Upgrade35_Test_[a-f0-9]{32}$'){
        Invoke-TestSql $container master "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END" | Out-Null
    }
    foreach($entry in $saved.GetEnumerator()){[Environment]::SetEnvironmentVariable($entry.Key,$entry.Value)}
    Pop-Location
}
