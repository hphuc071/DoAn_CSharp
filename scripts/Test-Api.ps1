#Requires -Version 7.0
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$passwordLine=[IO.File]::ReadLines((Join-Path $root '.env')) | Where-Object { $_.StartsWith('ADMIN_INITIAL_PASSWORD=') } | Select-Object -First 1
$adminPassword=$passwordLine.Substring('ADMIN_INITIAL_PASSWORD='.Length).Trim()
$keyLine=[IO.File]::ReadLines((Join-Path $root '.env')) | Where-Object { $_.StartsWith('FOURCHANG_POS_KEY=') } | Select-Object -First 1
$posKey=$keyLine.Substring('FOURCHANG_POS_KEY='.Length).Trim()
$base='http://localhost:5181'
function Request($method,$path,$body=$null,$headers=@{}) {
    $arguments=@{Method=$method;Uri=$base+$path;Headers=$headers;ContentType='application/json';SkipHttpErrorCheck=$true}
    if($null -ne $body) { $arguments.Body=ConvertTo-Json $body -Depth 8 -Compress }
    return Invoke-WebRequest @arguments
}
function Expect($response,$status,$label){if([int]$response.StatusCode -ne $status){throw "$label failed: $($response.StatusCode)"};Write-Output "PASS: $label"}
Expect (Request GET '/health/ready') 200 'API ready / 34 tables'
Expect (Request GET '/api/admin/employees') 401 'Anonymous cannot read admin data'
Expect (Request GET '/api/pos/employees') 401 'Unknown terminal cannot read POS employee list'
$login=Request POST '/api/auth/admin/login' @{TenDangNhap='quanly';MatKhau=$adminPassword};Expect $login 200 'Admin signs in using hashed SQL account'
$admin=$login.Content|ConvertFrom-Json;$adminHeader=@{Authorization='Bearer '+$admin.token}
Expect (Request GET '/api/admin/dashboard' $null $adminHeader) 200 'Dashboard reads SQL'
$stamp=[Guid]::NewGuid().ToString('N').Substring(0,12)
$employeeInput=@{HoTen='Nhân viên kiểm thử '+$stamp;SoDienThoai=$null;NgayVaoLam='2026-10-31T00:00:00Z';TrangThai='ACTIVE'}
$created=Request POST '/api/admin/employees' $employeeInput $adminHeader;Expect $created 200 'Create employee profile without login account'
$employee=$created.Content|ConvertFrom-Json
$employeeInput.HoTen+=' đã sửa';$employeeInput.PhienBanDuLieu=$employee.phienBanDuLieu
$updated=Request PUT ('/api/admin/employees/'+$employee.maNhanVien) $employeeInput $adminHeader;Expect $updated 200 'Update employee profile'
Expect (Request PUT ('/api/admin/employees/'+$employee.maNhanVien) $employeeInput $adminHeader) 409 'Reject stale employee rowversion'
$employee=$updated.Content|ConvertFrom-Json
$deviceHeader=@{'X-POS-Key'=$posKey}
$opened=Request POST '/api/pos/shifts' @{MaNhanVien=$employee.maNhanVien;TenCa='Sáng';MaMay='TEST-'+$stamp;TienMatDauCa=500000} $deviceHeader;Expect $opened 200 'Open shift by selecting employee / no staff login'
$shift=$opened.Content|ConvertFrom-Json;$staffHeader=@{Authorization='Bearer '+$shift.token}
Expect (Request GET '/api/admin/dashboard' $null $staffHeader) 403 'Staff cannot access manager dashboard'
Expect (Request GET '/api/pos/data/DonHang' $null $staffHeader) 200 'Staff can read orders with active shift'
$resume=Request POST '/api/pos/shifts' @{MaNhanVien=$employee.maNhanVien;TenCa='Sáng';MaMay='TEST-'+$stamp;TienMatDauCa=500000} $deviceHeader;Expect $resume 200 'Resume same employee and terminal without duplicate shift'
if(($resume.Content|ConvertFrom-Json).shiftId -ne $shift.shiftId){throw 'Duplicate shift created'}
Expect (Request POST '/api/pos/shifts/current/close' @{TienMatThucDem=0;LyDoChenhLech=''} $staffHeader) 400 'Closing cash discrepancy requires reason'
Expect (Request POST '/api/pos/shifts/current/close' @{TienMatThucDem=500000;LyDoChenhLech=''} $staffHeader) 200 'Close shift and persist cash count'
Expect (Request GET '/api/pos/data/DonHang' $null $staffHeader) 401 'Closed shift capability is revoked'
$employeeInput.TrangThai='INACTIVE';$employeeInput.PhienBanDuLieu=$employee.phienBanDuLieu
Expect (Request PUT ('/api/admin/employees/'+$employee.maNhanVien) $employeeInput $adminHeader) 200 'Deactivate test employee, preserve history'
$register=Request POST '/api/auth/customer/register' @{TenDangNhap='test'+$stamp;HoTen='Khách kiểm thử';Email=('test'+$stamp+'@example.test');SoDienThoai=$null;MatKhau='Test-only-'+[Guid]::NewGuid().ToString('N')+'!'}
Expect $register 200 'Customer registration persists separate account'
$customer=$register.Content|ConvertFrom-Json;$customerHeader=@{Authorization='Bearer '+$customer.token}
Expect (Request GET '/api/customer/profile' $null $customerHeader) 200 'Customer reads own SQL profile'
Expect (Request GET '/api/admin/data/TaiKhoanKhachHang' $null $customerHeader) 403 'Customer cannot list all accounts'
Expect (Request GET '/api/customer/orders' $null $customerHeader) 200 'Customer reads own order history'
$accounts=Request GET '/api/admin/data/TaiKhoanKhachHang' $null $adminHeader;Expect $accounts 200 'Manager reads customer list'
if($accounts.Content -match 'MatKhauBam|PBKDF2|MaLamMoiDaBam'){throw 'Secret field exposed'}
Write-Output 'PASS: table API does not expose password/token hashes'
Expect (Request POST '/api/auth/logout' $null $customerHeader) 200 'Customer logout persists session revocation'
Expect (Request GET '/api/customer/profile' $null $customerHeader) 401 'Logged-out token cannot be reused'
Expect (Request POST '/api/auth/logout' $null $adminHeader) 200 'Manager logout persists session revocation'
Write-Output 'HTTP INTEGRATION PASSED. Test employee is inactive; test shift is closed; no payments or stock movements created.'
