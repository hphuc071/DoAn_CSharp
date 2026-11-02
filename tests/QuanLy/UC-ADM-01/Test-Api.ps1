#Requires -Version 7.0
param([ValidateRange(1024,65532)][int]$Port = 5183, [switch]$KeepDatabase,
    [switch]$IncludePresentation, [switch]$IncludeRegression)
Import-Module (Join-Path $PSScriptRoot '../../DungChung/TestSupport.psm1') -Force
$ErrorActionPreference = 'Stop'
$root = Get-TestProjectRoot -StartPath $PSScriptRoot
$apiDll = Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll'
if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Build FourChang.sln trước khi kiểm thử.' }
$stamp = [Guid]::NewGuid().ToString('N')
$database = 'FourChang_UcAdm01_Test_' + $stamp
$base = "http://localhost:$Port"
$artifacts = Get-TestArtifactsDirectory -ProjectRoot $root -Actor QuanLy -UseCase UC-ADM-01 -RunId $stamp
$savedEnvironment = @{}
foreach ($key in @('FOURCHANG_DATABASE','ConnectionStrings__FourChang','SQLCMDPASSWORD')) {
    $savedEnvironment[$key] = [Environment]::GetEnvironmentVariable($key)
}
$local = Get-TestLocalSettings -ProjectRoot $root
$env:FOURCHANG_DATABASE = $database
Remove-Item Env:ConnectionStrings__FourChang -ErrorAction SilentlyContinue
$env:SQLCMDPASSWORD = $local['SQL_SA_PASSWORD']
$container = (& docker compose -f (Join-Path $root 'compose.yaml') ps -q sqlserver | Select-Object -First 1)
if (-not $container) { throw 'SQL Server Docker chưa chạy.' }
$script:passed = 0
$server = $null
function Check([bool]$condition, [string]$label) {
    if (-not $condition) { throw "FAIL: $label" }
    $script:passed++; Write-Output "PASS: $label"
}
function Request($method, $path, $body = $null, $headers = @{}) {
    Invoke-TestApiRequest -BaseUrl $base -Method $method -Path $path -Body $body -Headers $headers
}
function Expect($response, $status, $label) { Check ([int]$response.StatusCode -eq $status) $label }
function Sql([string]$query, [string]$db = $database) {
    Invoke-TestSql -ContainerId $container -Database $db -Query $query
}
function GetAccount([long]$id) { (Read-TestJson (Request GET "/api/admin/customers/$id" $null $script:admin)).account }
function ActionInput($account, $extra = @{}) {
    $inputBody = @{ MaThaoTac=[Guid]::NewGuid().ToString(); PhienBanDuLieu=$account.phienBanDuLieu; LyDo='Kiểm thử UC-ADM-01' }
    foreach ($entry in $extra.GetEnumerator()) { $inputBody[$entry.Key] = $entry.Value }
    $inputBody
}
function CreateInput($suffix) {
    @{ MaThaoTac=[Guid]::NewGuid().ToString(); TenDangNhap='ucadm01'+$stamp.Substring(0,8)+$suffix; HoTen='Khách kiểm thử '+$suffix; Email=('ucadm01'+$stamp+$suffix+'@example.test'); SoDienThoai=$null; MatKhauTam=('Temp-'+[Guid]::NewGuid().ToString('N')+'!') }
}
Push-Location $root
try {
    & dotnet $apiDll --init-db
    if ($LASTEXITCODE -ne 0) { throw 'Khởi tạo database kiểm thử thất bại.' }
    $server = Start-Process -FilePath dotnet -ArgumentList @('"'+$apiDll+'"','--urls',$base) -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $artifacts 'api.stdout.log') -RedirectStandardError (Join-Path $artifacts 'api.stderr.log')
    $ready = $false
    for ($i=0; $i -lt 50; $i++) {
        try { if ((Invoke-WebRequest "$base/health/ready" -TimeoutSec 2).StatusCode -eq 200) { $ready=$true; break } } catch { }
        Start-Sleep -Milliseconds 300
    }
    Check $ready 'API sẵn sàng với database kiểm thử riêng'
    Check ((Sql 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0').Trim() -eq '35') 'Giữ đúng 35 bảng'
    Check ((Sql "EXECUTE AS USER='fourchang_app'; SELECT CASE WHEN HAS_PERMS_BY_NAME('dbo.TaiKhoanKhachHang','OBJECT','DELETE')=1 AND HAS_PERMS_BY_NAME('dbo.DonHang','OBJECT','DELETE')=0 AND HAS_PERMS_BY_NAME('dbo.NhatKyHeThong','OBJECT','DELETE')=0 THEN 1 ELSE 0 END; REVERT;").Trim() -eq '1') 'DELETE chỉ được cấp cho tài khoản khách hàng'
    Expect (Request GET '/api/admin/customers') 401 'Từ chối truy cập quản trị khi chưa đăng nhập'
    $login = Request POST '/api/auth/admin/login' @{ TenDangNhap='quanly'; MatKhau=$local['ADMIN_INITIAL_PASSWORD'] }
    Expect $login 200 'Quản lý đăng nhập'
    $script:admin = @{Authorization='Bearer '+(Read-TestJson $login).token}
    Expect (Request GET '/api/admin/customers?page=0' $null $admin) 400 'Chặn trang không hợp lệ'
    Expect (Request GET '/api/admin/customers?pageSize=101' $null $admin) 400 'Chặn kích thước trang vượt giới hạn'
    $invalid = CreateInput 'invalid'; $invalid.Email='sai-email'
    Expect (Request POST '/api/admin/customers' $invalid $admin) 400 'Chặn email sai'
    $invalid.Email=$null
    Expect (Request POST '/api/admin/customers' $invalid $admin) 400 'Bắt buộc ít nhất một định danh liên hệ'
    $invalid.Email='valid@example.test'; $invalid.MatKhauTam='short'
    Expect (Request POST '/api/admin/customers' $invalid $admin) 400 'Chặn mật khẩu tạm ngắn'

    $create = CreateInput 'a'; $create.Email='  '+$create.Email.ToUpperInvariant()+'  '
    $created = Request POST '/api/admin/customers' $create $admin
    Expect $created 200 'Tạo tài khoản khách hàng bằng mật khẩu tạm'
    $customer = (Read-TestJson $created).account; $id=[long]$customer.maKhachHang
    Check ($customer.batBuocDoiMatKhau -and $customer.email -ceq $create.Email.Trim().ToLowerInvariant()) 'Chuẩn hóa email và buộc đổi mật khẩu'
    $replayed = Request POST '/api/admin/customers' $create $admin
    Expect $replayed 200 'Gửi lại cùng mã trả kết quả đã lưu'
    Check ((Read-TestJson $replayed).account.maKhachHang -eq $id) 'Không tạo tài khoản thứ hai khi gửi lặp'
    $different = $create.Clone(); $different.HoTen='Nội dung khác'
    Expect (Request POST '/api/admin/customers' $different $admin) 409 'Cùng mã khác nội dung bị từ chối'
    Expect (Request GET ('/api/admin/customers/operations/'+$create.MaThaoTac) $null $admin) 200 'Tra kết quả bằng mã thao tác'
    $duplicate = CreateInput 'duplicate'; $duplicate.Email=$customer.email
    Expect (Request POST '/api/admin/customers' $duplicate $admin) 409 'Chặn email đã dùng'
    $search = Request GET ('/api/admin/customers?search='+$id+'&page=1&pageSize=1') $null $admin
    Expect $search 200 'Tìm theo mã và phân trang'
    Check ((Read-TestJson $search).total -eq 1) 'Tìm đúng tài khoản theo mã'
    $safeJson = (Request GET "/api/admin/customers/$id" $null $admin).Content
    Check ($safeJson -notmatch 'MatKhauBam|PBKDF2|MaLamMoiDaBam' -and -not $safeJson.Contains($create.MatKhauTam)) 'Phản hồi không lộ mật khẩu hoặc hash'

    $update = @{MaThaoTac=[Guid]::NewGuid().ToString(); TenDangNhap=$customer.tenDangNhap; HoTen='Khách đã sửa'; Email=$customer.email; SoDienThoai='+84912345678'; PhienBanDuLieu=$customer.phienBanDuLieu}
    $updated = Request PUT "/api/admin/customers/$id" $update $admin
    Expect $updated 200 'Sửa hồ sơ bằng phiên bản hiện tại'
    $stale=$update.Clone();$stale.MaThaoTac=[Guid]::NewGuid().ToString()
    $conflict=Request PUT "/api/admin/customers/$id" $stale $admin
    Expect $conflict 409 'Chặn ghi đè bằng rowversion cũ'
    Check ($null -ne (Read-TestJson $conflict).current) 'Xung đột trả hồ sơ mới để đối chiếu'

    # Hai lần tạo cùng mã thực sự chạy đồng thời qua HTTP.
    $parallel=CreateInput 'parallel'; $http=[System.Net.Http.HttpClient]::new(); $http.DefaultRequestHeaders.Authorization=[System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',(Read-TestJson $login).token)
    try {
        $json=ConvertTo-Json $parallel -Compress
        $body1=[System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
        $body2=[System.Net.Http.StringContent]::new($json,[Text.Encoding]::UTF8,'application/json')
        $t1=$http.PostAsync($base+'/api/admin/customers',$body1);$t2=$http.PostAsync($base+'/api/admin/customers',$body2)
        $r1=$t1.GetAwaiter().GetResult();$r2=$t2.GetAwaiter().GetResult()
        Check ([int]$r1.StatusCode -eq 200 -and [int]$r2.StatusCode -eq 200) 'Hai yêu cầu cùng mã đồng thời cùng thành công'
        $a1=$r1.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
        $a2=$r2.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json
        Check ($a1.account.maKhachHang -eq $a2.account.maKhachHang) 'Cùng mã đồng thời chỉ tạo một khách'
    } finally { $http.Dispose() }

    # Phân trang nhiều hơn một trang, không chỉ kiểm tra trường page trong JSON.
    for ($i=0;$i -lt 21;$i++) {
        $pageAccount=Request POST '/api/admin/customers' (CreateInput "page$i") $admin
        if ([int]$pageAccount.StatusCode -ne 200) { throw 'Không tạo được dữ liệu kiểm thử phân trang.' }
    }
    $page1=Read-TestJson (Request GET '/api/admin/customers?page=1&pageSize=20' $null $admin)
    $page2=Read-TestJson (Request GET '/api/admin/customers?page=2&pageSize=20' $null $admin)
    Check ($page1.items.Count -eq 20 -and $page2.items.Count -gt 0) 'Phân trang thực tế với hơn 20 tài khoản'
    Check (@($page1.items.maKhachHang | Where-Object { $_ -in $page2.items.maKhachHang }).Count -eq 0) 'Hai trang không lặp tài khoản'

    $deviceKey='device-'+[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
    $registration=Request POST '/api/admin/access/devices' @{MaThaoTac=[Guid]::NewGuid().ToString();MaMay='UCADM01-TEST';TenMay='Máy fixture UC-ADM-01';KhoaThietBi=$deviceKey;LyDo='Chuẩn bị test quyền POS'} $admin
    if([int]$registration.StatusCode -ne 200){throw 'Không đăng ký được máy fixture.'}
    $device=@{'X-POS-Key'=$deviceKey;'X-POS-Machine'='UCADM01-TEST'}
    $employee=(Read-TestJson (Request GET '/api/pos/employees' $null $device))[0]
    $shift=Request POST '/api/pos/shifts' @{MaNhanVien=$employee.maNhanVien;TenCa='Sáng';MaMay='UCADM01-TEST';TienMatDauCa=0} $device
    Expect $shift 200 'Mở ca POS kiểm tra ranh giới quyền'
    $staff=@{Authorization='Bearer '+(Read-TestJson $shift).token}
    Expect (Request GET '/api/admin/customers' $null $staff) 403 'Nhân viên không được quản lý tài khoản khách'
    Expect (Request POST '/api/pos/shifts/current/close' @{TienMatThucDem=0;LyDoChenhLech=''} $staff) 200 'Đóng ca kiểm thử'

    $temporaryLogin = Request POST '/api/auth/customer/login' @{TenDangNhap=$create.TenDangNhap;MatKhau=$create.MatKhauTam}
    Expect $temporaryLogin 200 'Khách đăng nhập bằng mật khẩu tạm'
    $temporary=Read-TestJson $temporaryLogin; $limited=@{Authorization='Bearer '+$temporary.token}
    Check $temporary.mustChangePassword 'Phiên tạm có cờ bắt buộc đổi mật khẩu'
    Expect (Request GET '/api/customer/profile' $null $limited) 403 'Phiên tạm không đọc hồ sơ nghiệp vụ'
    Expect (Request GET '/api/customer/orders' $null $limited) 403 'Phiên tạm không đọc đơn'
    Expect (Request GET '/api/admin/customers' $null $limited) 403 'Khách không truy cập quản trị'
    Expect (Request GET '/api/auth/customer/temporary-password' $null $limited) 200 'Phiên tạm chỉ vào luồng đổi mật khẩu'
    Expect (Request POST '/api/auth/customer/temporary-password' @{MatKhauMoi=$create.MatKhauTam} $limited) 400 'Mật khẩu mới không trùng mật khẩu tạm'
    $newPassword='New-'+[Guid]::NewGuid().ToString('N')+'!'
    Expect (Request POST '/api/auth/customer/temporary-password' @{MatKhauMoi=$newPassword} $limited) 200 'Khách đổi mật khẩu tạm'
    Expect (Request GET '/api/auth/customer/temporary-password' $null $limited) 401 'Phiên đổi mật khẩu đã bị thu hồi'
    $fullLogin=Request POST '/api/auth/customer/login' @{TenDangNhap=$create.TenDangNhap;MatKhau=$newPassword}
    Expect $fullLogin 200 'Đăng nhập lại bằng mật khẩu mới'
    $full=@{Authorization='Bearer '+(Read-TestJson $fullLogin).token}
    Expect (Request GET '/api/customer/profile' $null $full) 200 'Phiên đầy đủ được dùng hồ sơ'
    Expect (Request GET '/api/admin/customers' $null $full) 403 'Khách có phiên đầy đủ vẫn không quản trị'
    $current=GetAccount $id
    $missingReason=ActionInput $current @{TrangThai='LOCKED';LyDo=''}
    Expect (Request POST "/api/admin/customers/$id/status" $missingReason $admin) 400 'Khóa tài khoản bắt buộc lý do'
    Expect (Request POST "/api/admin/customers/$id/status" (ActionInput $current @{TrangThai='LOCKED'}) $admin) 200 'Khóa tài khoản'
    Expect (Request GET '/api/customer/profile' $null $full) 401 'Khóa thu hồi phiên đang dùng'
    Expect (Request POST '/api/auth/customer/login' @{TenDangNhap=$create.TenDangNhap;MatKhau=$newPassword}) 401 'Khách bị khóa không đăng nhập'
    Expect (Request POST "/api/admin/customers/$id/status" (ActionInput (GetAccount $id) @{TrangThai='ACTIVE'}) $admin) 200 'Mở lại tài khoản'
    Expect (Request GET '/api/customer/profile' $null $full) 401 'Mở khóa không khôi phục token đã thu hồi'
    $fullLogin=Request POST '/api/auth/customer/login' @{TenDangNhap=$create.TenDangNhap;MatKhau=$newPassword}
    $full=@{Authorization='Bearer '+(Read-TestJson $fullLogin).token}
    $resetPassword='Reset-'+[Guid]::NewGuid().ToString('N')+'!'
    Expect (Request POST "/api/admin/customers/$id/reset-password" (ActionInput (GetAccount $id) @{MatKhauTam=$resetPassword}) $admin) 200 'Reset cấp mật khẩu tạm mới'
    Expect (Request GET '/api/customer/profile' $null $full) 401 'Reset thu hồi phiên cũ'
    $resetLogin=Request POST '/api/auth/customer/login' @{TenDangNhap=$create.TenDangNhap;MatKhau=$resetPassword}
    Expect $resetLogin 200 'Mật khẩu reset đăng nhập được'
    Check (Read-TestJson $resetLogin).mustChangePassword 'Reset bắt buộc đổi lại mật khẩu'

    $deletableInput=CreateInput 'deletable';$deletable=(Read-TestJson (Request POST '/api/admin/customers' $deletableInput $admin)).account
    $deletableId=[long]$deletable.maKhachHang
    Check (Read-TestJson (Request GET "/api/admin/customers/$deletableId" $null $admin)).coTheXoa 'Khách chưa tham chiếu có thể xóa'
    $delete=ActionInput $deletable
    Expect (Request DELETE "/api/admin/customers/$deletableId" $delete $admin) 200 'Xóa khách chưa có tham chiếu'
    Expect (Request GET "/api/admin/customers/$deletableId" $null $admin) 404 'Khách đã xóa không còn trong danh sách'
    Expect (Request DELETE "/api/admin/customers/$deletableId" $delete $admin) 200 'Thử lại xóa không tạo lịch sử trùng'

    $historyInput=CreateInput 'history';$history=(Read-TestJson (Request POST '/api/admin/customers' $historyInput $admin)).account;$historyId=[long]$history.maKhachHang
    Sql @"
DECLARE @o bigint,@p bigint;
INSERT dbo.DonHang(MaDonHienThi,MaKhachHang,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('UCADM01-HISTORY',$historyId,'WEB','TAKEAWAY',0,0,'CANCELLED'); SET @o=SCOPE_IDENTITY();
INSERT dbo.ThanhToan(MaThanhToanHienThi,MaDonHang,PhuongThuc,SoTienYeuCau,SoTienDaXacMinh,TrangThai,MaThaoTac) VALUES('UCADM01-PAYMENT',@o,'BANK_TRANSFER',45000,45000,'VERIFIED',NEWID()); SET @p=SCOPE_IDENTITY();
INSERT dbo.HoaDon(SoHoaDon,MaDonHang,LoaiHoaDon,SoTien,NoiDungHoaDonDaChotJson,TrangThai,PhatHanhLuc,MaQuanLyPhatHanh) VALUES('UCADM01-INVOICE',@o,'ORIGINAL',45000,N'{}','ISSUED',SYSUTCDATETIME(),1);
INSERT dbo.HoSoHoanTien(MaHoanTienHienThi,MaDonHang,MaThanhToan,LyDoHoan,SoTienYeuCau,PhuongThucHoan,TrangThai,MaKhachHangYeuCau) VALUES('UCADM01-REFUND',@o,@p,N'Kiểm thử bảo toàn nghĩa vụ',45000,'BANK_TRANSFER','REQUESTED',$historyId);
"@ | Out-Null
    $detail=Read-TestJson (Request GET "/api/admin/customers/$historyId" $null $admin)
    Check (-not $detail.coTheXoa -and $detail.giaoDichGanDay.Count -eq 4) 'Hồ sơ hiện đủ tham chiếu đơn, thu, hóa đơn và hoàn'
    Expect (Request DELETE "/api/admin/customers/$historyId" (ActionInput $detail.account) $admin) 409 'Chặn xóa khách đã có lịch sử'
    Expect (Request POST "/api/admin/customers/$historyId/status" (ActionInput $detail.account @{TrangThai='INACTIVE'}) $admin) 200 'Cho ngừng tài khoản có lịch sử'
    Check ((Sql "SELECT CASE WHEN EXISTS(SELECT 1 FROM dbo.DonHang WHERE MaKhachHang=$historyId AND TrangThai='CANCELLED') AND EXISTS(SELECT 1 FROM dbo.ThanhToan WHERE MaThanhToanHienThi='UCADM01-PAYMENT' AND SoTienDaXacMinh=45000) AND EXISTS(SELECT 1 FROM dbo.HoaDon WHERE SoHoaDon='UCADM01-INVOICE' AND TrangThai='ISSUED') AND EXISTS(SELECT 1 FROM dbo.HoSoHoanTien WHERE MaHoanTienHienThi='UCADM01-REFUND' AND SoTienYeuCau=45000 AND TrangThai='REQUESTED') THEN 1 ELSE 0 END").Trim() -eq '1') 'Ngừng khách không đổi đơn, tiền, hóa đơn hoặc khoản phải hoàn'

    # E6: chủ động làm audit thất bại để chứng minh tài khoản không commit dở dang.
    $rollback=GetAccount $id
    $rollbackInput=@{MaThaoTac=[Guid]::NewGuid().ToString();TenDangNhap=$rollback.tenDangNhap;HoTen='KHONG DUOC LUU';Email=$rollback.email;SoDienThoai=$rollback.soDienThoai;PhienBanDuLieu=$rollback.phienBanDuLieu}
    Sql "DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app" | Out-Null
    try {
        $failed=Request PUT "/api/admin/customers/$id" $rollbackInput $admin
        Check ([int]$failed.StatusCode -ge 500 -or [int]$failed.StatusCode -eq 409) 'Ghi audit lỗi không báo lưu thành công'
    } finally { Sql "REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app" | Out-Null }
    Check ((GetAccount $id).hoTen -eq $rollback.hoTen) 'Audit lỗi rollback thay đổi hồ sơ'
    Expect (Request GET ('/api/admin/customers/operations/'+$rollbackInput.MaThaoTac) $null $admin) 404 'Audit lỗi không có kết quả thành công giả'
    Check ((Sql "SELECT COUNT(*) FROM dbo.NhatKyHeThong WHERE MaThaoTac='$($create.MaThaoTac)'").Trim() -eq '1') 'Mã thao tác chỉ có một audit'
    $auditJson=Sql "SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong WHERE HanhDong LIKE 'CUSTOMER_%'"
    Check ($auditJson -notmatch 'PBKDF2|MatKhauBam' -and -not $auditJson.Contains($create.MatKhauTam) -and -not $auditJson.Contains($resetPassword)) 'Audit không lưu mật khẩu tạm hoặc hash mật khẩu'
    Expect (Request POST '/api/auth/logout' $null $admin) 200 'Quản lý đăng xuất'
    Expect (Request GET '/api/admin/customers' $null $admin) 401 'Phiên Quản lý hết hiệu lực bị từ chối'
    if ($IncludePresentation) {
        & (Join-Path $PSScriptRoot 'Test-Presentation.ps1') -BaseUrl $base -WebPort ($Port+1) -ProxyPort ($Port+2) -OutputDirectory (Join-Path $artifacts 'presentation')
        if (-not $?) { throw 'Kiểm tra giao diện thất bại.' }
    }
    if ($IncludeRegression) {
        & (Join-Path $root 'tests/TichHop/Test-Api.ps1') -BaseUrl $base
        if (-not $?) { throw 'Kiểm tra hồi quy thất bại.' }
    }
    Write-Output "UC-ADM-01 PASSED: $script:passed checks. Database kiểm thử: $database"
    @{passed=$script:passed;apiPassed=$script:passed;winFormsPassed=$(if($IncludePresentation){16}else{0});webPassed=$(if($IncludePresentation){8}else{0});regressionPassed=$(if($IncludeRegression){25}else{0});database=$database;tables=35;port=$Port} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'result.json')
} finally {
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force; $server.WaitForExit() }
    # Chỉ xóa database do chính lần chạy này tạo; tên phải trùng tuyệt đối và đúng tiền tố kiểm thử.
    if (-not $KeepDatabase -and $database -cmatch '^FourChang_UcAdm01_Test_[a-f0-9]{32}$') {
        try { Sql "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END" 'master' | Out-Null } catch { Write-Warning 'Không dọn được database kiểm thử; kiểm tra Docker.' }
    }
    foreach ($entry in $savedEnvironment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value) }
    Pop-Location
}
