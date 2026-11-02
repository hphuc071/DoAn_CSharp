#Requires -Version 7.0
param([ValidateRange(1024,65532)][int]$Port = 5183, [switch]$KeepDatabase,
    [switch]$IncludePresentation, [switch]$IncludeRegression)
Import-Module (Join-Path $PSScriptRoot '../../DungChung/TestSupport.psm1') -Force
$ErrorActionPreference = 'Stop'
$root = Get-TestProjectRoot -StartPath $PSScriptRoot
$apiDll = Join-Path $root 'src/FourChang.Api/bin/Debug/net8.0/FourChang.Api.dll'
if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Build FourChang.sln trước khi kiểm thử.' }
$stamp = [Guid]::NewGuid().ToString('N')
$database = 'FourChang_UcAdm02_Test_' + $stamp
$base = "http://localhost:$Port"
$artifacts = Get-TestArtifactsDirectory -ProjectRoot $root -Actor QuanLy -UseCase UC-ADM-02 -RunId $stamp
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
    $inputBody = @{ MaThaoTac=[Guid]::NewGuid().ToString(); PhienBanDuLieu=$account.phienBanDuLieu; LyDo='Kiểm thử UC-ADM-02' }
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

    Check $ready 'Backend sẵn sàng với 35 bảng trong database tạm'
    Check ((Sql 'SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0').Trim() -eq '35') 'Database có đúng 35 bảng nghiệp vụ'
    Expect (Request GET '/api/admin/access/account') 401 'Ẩn danh không đọc tài khoản Admin'
    Expect (Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau='Sai-mat-khau-test'}) 401 'Sai mật khẩu không cấp phiên'
    $originalPassword=$local['ADMIN_INITIAL_PASSWORD']
    $login=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$originalPassword};Expect $login 200 'Quản lý duy nhất đăng nhập'
    $session=Read-TestJson $login;$admin=@{Authorization='Bearer '+$session.token};$script:admin=$admin
    $secondLogin=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$originalPassword}
    $second=@{Authorization='Bearer '+(Read-TestJson $secondLogin).token}
    $profile=Read-TestJson (Request GET '/api/admin/access/account' $null $admin)
    Check ($profile.maQuanLy -eq 1 -and -not $profile.batBuocDoiMatKhau) 'Hồ sơ chỉ có Quản lý số 1'
    Expect (Request POST '/api/admin/access/account' @{TenDangNhap='admin2'} $admin) 405 'Không có chức năng tạo Admin thứ hai'
    Check ((Sql 'SELECT COUNT(*) FROM dbo.TaiKhoanQuanLy').Trim() -eq '1') 'Không sinh thêm tài khoản Quản lý'
    function Profile {Read-TestJson (Request GET '/api/admin/access/account' $null $script:admin)}
    function PasswordBody($old,$new) {@{MaThaoTac=[Guid]::NewGuid().ToString();PhienBanDuLieu=(Profile).phienBanDuLieu;MatKhauHienTai=$old;MatKhauMoi=$new;XacNhanMatKhau=$new}}
    $newPassword='Admin-'+[Guid]::NewGuid().ToString('N')+'!'
    $bad=PasswordBody 'Khong-dung-mat-khau' $newPassword
    Expect (Request POST '/api/auth/admin/password' $bad $admin) 400 'Đổi mật khẩu kiểm tra mật khẩu hiện tại'
    $bad=PasswordBody $originalPassword 'short'
    Expect (Request POST '/api/auth/admin/password' $bad $admin) 400 'Mật khẩu mới phải đạt độ dài'
    $bad=PasswordBody $originalPassword $newPassword;$bad.XacNhanMatKhau+='khac'
    Expect (Request POST '/api/auth/admin/password' $bad $admin) 400 'Xác nhận mật khẩu phải khớp'
    $stale=PasswordBody $originalPassword $newPassword;$stale.PhienBanDuLieu=[Convert]::ToBase64String([byte[]](0,0,0,0,0,0,0,1))
    Expect (Request POST '/api/auth/admin/password' $stale $admin) 409 'Version cũ không được đổi mật khẩu'

    $deviceKey='device-'+[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
    $register=@{MaThaoTac=[Guid]::NewGuid().ToString();MaMay='POS-UCADM02';TenMay='Quầy kiểm thử';KhoaThietBi=$deviceKey;LyDo='Đăng ký máy kiểm thử'}
    $noReason=$register.Clone();$noReason.MaThaoTac=[Guid]::NewGuid().ToString();$noReason.LyDo=''
    Expect (Request POST '/api/admin/access/devices' $noReason $admin) 400 'Đăng ký máy cần lý do'
    $created=Request POST '/api/admin/access/devices' $register $admin;Expect $created 200 'Đăng ký thiết bị POS'
    $device=(Read-TestJson $created).device;$deviceId=[long]$device.maThietBiPOS
    $replay=Request POST '/api/admin/access/devices' $register $admin;Expect $replay 200 'Gửi lại đăng ký trả kết quả cũ'
    Check ((Read-TestJson $replay).device.maThietBiPOS -eq $deviceId) 'Không tạo thiết bị trùng khi gửi lặp'
    $changed=$register.Clone();$changed.TenMay='Nội dung khác'
    Expect (Request POST '/api/admin/access/devices' $changed $admin) 409 'Cùng mã thao tác khác nội dung bị từ chối'
    $deviceList=Request GET '/api/admin/access/devices' $null $admin
    Check (-not $deviceList.Content.Contains($deviceKey) -and $deviceList.Content -notmatch 'MaBamKhoa|MatKhauBam') 'Danh sách máy không lộ khóa hoặc hash'
    $unknown=@{'X-POS-Key'=$local['FOURCHANG_POS_KEY'];'X-POS-Machine'='CHUA-DANG-KY'}
    Expect (Request GET '/api/pos/employees' $null $unknown) 401 'Khóa toàn cục không mở được máy chưa đăng ký'
    $deviceHeader=@{'X-POS-Key'=$deviceKey;'X-POS-Machine'='POS-UCADM02'}
    $employees=Request GET '/api/pos/employees' $null $deviceHeader;Expect $employees 200 'Máy đã cấp được đọc danh sách nhân viên'
    $employee=(Read-TestJson $employees)[0]
    Expect (Request POST '/api/pos/shifts' @{MaNhanVien=$employee.maNhanVien;TenCa='Sáng';MaMay='MAY-KHAC';TienMatDauCa=500000} $deviceHeader) 403 'Không dùng khóa máy này để mở ca máy khác'
    $shift=Request POST '/api/pos/shifts' @{MaNhanVien=$employee.maNhanVien;TenCa='Sáng';MaMay='POS-UCADM02';TienMatDauCa=500000} $deviceHeader
    Expect $shift 200 'Mở ca bằng tên nhân viên và thiết bị, không tài khoản Staff'
    $shiftInfo=Read-TestJson $shift;$shiftId=[long]$shiftInfo.shiftId;$staff=@{Authorization='Bearer '+$shiftInfo.token}
    Expect (Request GET '/api/admin/access/account' $null $staff) 403 'Staff không đọc tài khoản quản trị'
    Expect (Request POST '/api/auth/admin/password' (PasswordBody $originalPassword $newPassword) $staff) 403 'Staff không đổi mật khẩu Admin'
    Expect (Request POST '/api/admin/access/devices' $register $staff) 403 'Staff không cấp thiết bị'
    $customerInput=@{TenDangNhap='customer'+$stamp.Substring(0,12);HoTen='Khách kiểm thử quyền';Email=$stamp+'@example.test';MatKhau='Customer-'+[Guid]::NewGuid().ToString('N')+'!'}
    $customer=Read-TestJson (Request POST '/api/auth/customer/register' $customerInput)
    Expect (Request GET '/api/admin/access/devices' $null @{Authorization='Bearer '+$customer.token}) 403 'Khách không đọc cấu hình POS'

    $passwordInput=PasswordBody $originalPassword $newPassword
    Expect (Request POST '/api/auth/admin/password' $passwordInput $admin) 200 'Đổi mật khẩu Admin thành công'
    Expect (Request GET '/api/admin/access/account' $null $admin) 401 'Đổi mật khẩu thu hồi phiên hiện tại'
    Expect (Request GET '/api/admin/access/account' $null $second) 401 'Đổi mật khẩu thu hồi phiên Admin khác'
    Expect (Request GET ('/api/auth/admin/password-operations/'+$passwordInput.MaThaoTac) $null $admin) 200 'Token nguồn đã thu hồi chỉ đọc biên nhận đổi mật khẩu của mình'
    Expect (Request GET ('/api/auth/admin/password-operations/'+$passwordInput.MaThaoTac) $null $second) 401 'Phiên khác không đọc biên nhận riêng'
    Expect (Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$originalPassword}) 401 'Mật khẩu cũ hết hiệu lực'
    $login=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$newPassword};Expect $login 200 'Mật khẩu mới đăng nhập được'
    $admin=@{Authorization='Bearer '+(Read-TestJson $login).token};$script:admin=$admin
    Expect (Request GET '/api/pos/data/DonHang' $null $staff) 200 'Đổi mật khẩu Admin không chốt ca POS'
    Expect (Request POST '/api/auth/logout' $null $admin) 200 'Đăng xuất Admin ghi nhận thu hồi phiên'
    Expect (Request GET '/api/admin/access/account' $null $admin) 401 'Token đăng xuất không tái sử dụng'
    Expect (Request GET '/api/pos/data/DonHang' $null $staff) 200 'Đăng xuất Admin không kết thúc ca POS'

    Sql 'UPDATE dbo.TaiKhoanQuanLy SET BatBuocDoiMatKhau=1 WHERE MaQuanLy=1' | Out-Null
    $temporaryLogin=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$newPassword};Expect $temporaryLogin 200 'Admin có cờ mật khẩu tạm đăng nhập phiên hạn chế'
    $temporary=Read-TestJson $temporaryLogin;$tempHeader=@{Authorization='Bearer '+$temporary.token}
    Check $temporary.mustChangePassword 'Phiên Admin tạm có cờ buộc đổi'
    Expect (Request GET '/api/admin/dashboard' $null $tempHeader) 403 'Phiên Admin tạm không vào Dashboard'
    Expect (Request GET '/api/admin/access/devices' $null $tempHeader) 403 'Phiên Admin tạm không quản lý máy'
    $context=Read-TestJson (Request GET '/api/auth/admin/password-context' $null $tempHeader)
    Expect (Request POST '/api/auth/admin/password' @{MaThaoTac=[Guid]::NewGuid().ToString();PhienBanDuLieu=$context.phienBanDuLieu;MatKhauHienTai=$newPassword;MatKhauMoi=$originalPassword;XacNhanMatKhau=$originalPassword} $tempHeader) 200 'Đổi mật khẩu tạm rồi yêu cầu đăng nhập lại'
    Expect (Request GET '/api/auth/admin/password-context' $null $tempHeader) 401 'Phiên tạm đã đổi bị thu hồi'
    $login=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$originalPassword};$admin=@{Authorization='Bearer '+(Read-TestJson $login).token};$script:admin=$admin

    $revoke=@{MaThaoTac=[Guid]::NewGuid().ToString();PhienBanDuLieu=$device.phienBanDuLieu;LyDo='Thu hồi kiểm thử'}
    $revoked=Request POST "/api/admin/access/devices/$deviceId/revoke" $revoke $admin;Expect $revoked 200 'Thu hồi máy đang có ca'
    $device=(Read-TestJson $revoked).device
    Check ($device.soCaChuaKetThuc -eq 1) 'Thu hồi giữ ca liên quan để Admin xử lý'
    Expect (Request GET '/api/pos/data/DonHang' $null $staff) 401 'Token ca trên máy thu hồi bị chặn ngay'
    Expect (Request GET '/api/pos/employees' $null $deviceHeader) 401 'Máy đã thu hồi không lấy danh sách nhân viên'
    $grant=$register.Clone();$grant.MaThaoTac=[Guid]::NewGuid().ToString();$grant.PhienBanDuLieu=$device.phienBanDuLieu;$grant.KhoaThietBi='device-'+[Guid]::NewGuid().ToString('N')+[Guid]::NewGuid().ToString('N')
    Expect (Request POST '/api/admin/access/devices' $grant $admin) 409 'Chưa xử lý ca thì không cấp lại máy'
    $activeShifts=Read-TestJson (Request GET "/api/admin/access/devices/$deviceId/shifts" $null $admin)
    Check ($activeShifts[0].tienMatDuKien -eq 500000) 'Ca máy thu hồi hiển thị tiền dự kiến đúng'
    Sql "INSERT dbo.DonHang(MaDonHienThi,MaCaLamViec,KenhDatHang,HinhThucNhanMon,PhiGiaoHangBanDau,PhiGiaoHangHienTai,TrangThai) VALUES('UCADM02-PENDING',$shiftId,'POS','TAKEAWAY',0,0,'ACCEPTED')" | Out-Null
    $close=@{MaThaoTac=[Guid]::NewGuid().ToString();PhienBanDuLieu=$activeShifts[0].phienBanDuLieu;TienMatThucDem=500000;LyDo='Đối chiếu và chốt ca máy thu hồi'}
    Expect (Request POST "/api/admin/access/devices/$deviceId/shifts/$shiftId/close" $close $admin) 409 'Không chốt ca khi còn đơn chưa xử lý'
    Sql "UPDATE dbo.DonHang SET TrangThai='CANCELLED' WHERE MaDonHienThi='UCADM02-PENDING'" | Out-Null
    $close.MaThaoTac=[Guid]::NewGuid().ToString()
    Expect (Request POST "/api/admin/access/devices/$deviceId/shifts/$shiftId/close" $close $admin) 200 'Admin chốt ca liên quan khi đủ điều kiện'
    Check ((Sql "SELECT COUNT(*) FROM dbo.CaLamViec WHERE MaCaLamViec=$shiftId AND TrangThai='CLOSED' AND MaQuanLyDuyet=1 AND TienMatThucDem=500000").Trim() -eq '1') 'Chốt ca lưu tiền và người xác nhận, không đổi chủ ca'
    $grant.MaThaoTac=[Guid]::NewGuid().ToString()
    Expect (Request POST '/api/admin/access/devices' $grant $admin) 200 'Cấp lại máy sau xử lý ca'
    Expect (Request GET '/api/pos/employees' $null $deviceHeader) 401 'Khóa cũ không sống lại khi cấp lại máy'
    $deviceHeader['X-POS-Key']=$grant.KhoaThietBi
    Expect (Request GET '/api/pos/employees' $null $deviceHeader) 200 'Khóa mới dùng được sau cấp lại'
    $device=(Read-TestJson (Request GET '/api/admin/access/devices' $null $admin))[0]
    $staleRevoke=$revoke.Clone();$staleRevoke.MaThaoTac=[Guid]::NewGuid().ToString()
    Expect (Request POST "/api/admin/access/devices/$deviceId/revoke" $staleRevoke $admin) 409 'Version thiết bị cũ không ghi đè cấu hình mới'

    $atomic=@{MaThaoTac=[Guid]::NewGuid().ToString();PhienBanDuLieu=$device.phienBanDuLieu;LyDo='Thử audit rollback'}
    Sql 'DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app' | Out-Null
    try{$failed=Request POST "/api/admin/access/devices/$deviceId/revoke" $atomic $admin;Check ([int]$failed.StatusCode -ne 200) 'Audit lỗi không báo thu hồi thành công'}
    finally{Sql 'REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app' | Out-Null}
    Expect (Request GET '/api/pos/employees' $null $deviceHeader) 200 'Audit lỗi rollback trạng thái thiết bị'
    Expect (Request GET ('/api/admin/access/devices/operations/'+$atomic.MaThaoTac) $null $admin) 404 'Audit lỗi không có kết quả thành công giả'
    $atomicPassword=PasswordBody $originalPassword $newPassword
    Sql 'DENY INSERT ON OBJECT::dbo.NhatKyHeThong TO fourchang_app' | Out-Null
    try{$failed=Request POST '/api/auth/admin/password' $atomicPassword $admin;Check ([int]$failed.StatusCode -ne 200) 'Audit lỗi không báo đổi mật khẩu thành công'}
    finally{Sql 'REVOKE INSERT ON OBJECT::dbo.NhatKyHeThong FROM fourchang_app' | Out-Null}
    Expect (Request GET '/api/admin/access/account' $null $admin) 200 'Audit lỗi rollback thu hồi phiên Admin'
    $audit=Sql "SELECT CONCAT(COALESCE(DuLieuTruocJson,''),COALESCE(DuLieuSauJson,''),COALESCE(KetQuaDaChotJson,'')) FROM dbo.NhatKyHeThong"
    Check (-not $audit.Contains($originalPassword) -and -not $audit.Contains($newPassword) -and -not $audit.Contains($deviceKey) -and -not $audit.Contains($grant.KhoaThietBi) -and $audit -notmatch 'MatKhauBam|MaBamKhoa') 'Audit không lưu mật khẩu, khóa hoặc hash xác thực'
    for($i=0;$i -lt 5;$i++){if([int](Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau='Sai-mat-khau-test'}).StatusCode -ne 401){throw 'Kiểm tra khóa đăng nhập thất bại.'}}
    Check ((Sql "SELECT COUNT(*) FROM dbo.TaiKhoanQuanLy WHERE MaQuanLy=1 AND SoLanDangNhapSai>=5 AND KhoaDenLuc>SYSUTCDATETIME()").Trim() -eq '1') 'Sai mật khẩu liên tiếp khóa đăng nhập có thời hạn'
    Expect (Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$originalPassword}) 401 'Đúng mật khẩu nhưng đang tạm khóa vẫn không đăng nhập'
    Sql 'UPDATE dbo.TaiKhoanQuanLy SET KhoaDenLuc=NULL,SoLanDangNhapSai=0 WHERE MaQuanLy=1' | Out-Null
    if($IncludePresentation){& (Join-Path $PSScriptRoot 'Test-Presentation.ps1') -BaseUrl $base -OutputDirectory (Join-Path $artifacts 'presentation');if(-not $?){throw 'Test giao diện thất bại.'}}
    if($IncludeRegression){& (Join-Path $root 'tests/TichHop/Test-Api.ps1') -BaseUrl $base;if(-not $?){throw 'Hồi quy thất bại.'}}
    Write-Output "UC-ADM-02 PASSED: $script:passed checks."
    @{apiPassed=$script:passed;database=$database;tables=35;presentation=$IncludePresentation.IsPresent} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $artifacts 'result.json')
} finally {
    if ($server -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force; $server.WaitForExit() }
    # Chỉ xóa database do chính lần chạy này tạo; tên phải trùng tuyệt đối và đúng tiền tố kiểm thử.
    if (-not $KeepDatabase -and $database -cmatch '^FourChang_UcAdm02_Test_[a-f0-9]{32}$') {
        try { Sql "IF DB_ID(N'$database') IS NOT NULL BEGIN ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]; END" 'master' | Out-Null } catch { Write-Warning 'Không dọn được database kiểm thử; kiểm tra Docker.' }
    }
    foreach ($entry in $savedEnvironment.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key,$entry.Value) }
    Pop-Location
}
