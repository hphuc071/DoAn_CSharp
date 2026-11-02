// Luồng Web hỗ trợ yêu cầu mật khẩu tạm của UC-ADM-01; chủ sở hữu test vẫn là actor Quản lý.
const path = require('path');
const { chromium } = require('playwright');
const apiUrl=(process.env.FOURCHANG_TEST_API_URL || 'http://localhost:5183').replace(/\/$/,'');
const webUrl=(process.env.FOURCHANG_TEST_WEB_URL || 'http://localhost:5184').replace(/\/$/,'');
const adminPassword=process.env.FOURCHANG_TEST_ADMIN_PASSWORD;
const output=process.env.FOURCHANG_TEST_OUTPUT;
if(!adminPassword || !output) throw new Error('Chạy bằng Test-Presentation.ps1 để cấp môi trường kiểm thử.');
const {randomUUID}=require('crypto');
let passed=0;
function check(value,message){if(!value)throw new Error(message);passed++;console.log('PASS: '+message);}
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.FOURCHANG_TEST_CHROMIUM_PATH?{executablePath:process.env.FOURCHANG_TEST_CHROMIUM_PATH}:{})});
 try {
  const context=await browser.newContext({viewport:{width:1440,height:1000}});
  const login=await context.request.post(apiUrl+'/api/auth/admin/login',{data:{TenDangNhap:'quanly',MatKhau:adminPassword}});
  check(login.ok(),'Admin tạo khách phục vụ kiểm tra Web trong database tạm');
  const admin=await login.json();const headers={Authorization:'Bearer '+admin.token};
  const stamp=randomUUID().replaceAll('-','');const username='webqa'+stamp.slice(0,12);const temporary='Temp-'+randomUUID()+'!';const password='New-'+randomUUID()+'!';
  const created=await context.request.post(apiUrl+'/api/admin/customers',{headers,data:{MaThaoTac:randomUUID(),TenDangNhap:username,HoTen:'Khách kiểm tra đổi mật khẩu tạm',Email:username+'@example.test',SoDienThoai:null,MatKhauTam:temporary}});
  check(created.ok(),'Tài khoản Admin tạo có mật khẩu tạm');
  const page=await context.newPage();
  await page.goto(webUrl+'/Account/Login');
  await page.getByLabel('Tên đăng nhập, email hoặc số điện thoại').fill(username);
  await page.getByLabel('Mật khẩu',{exact:true}).fill(temporary);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();
  await page.waitForURL('**/Account/ChangeTemporaryPassword');
  check(true,'Đăng nhập Web bằng mật khẩu tạm chuyển đúng trang bắt buộc đổi');
  await page.screenshot({path:path.join(output,'web-temporary-password.png'),fullPage:true});
  await page.getByLabel('Mật khẩu mới',{exact:true}).fill(password);
  await page.getByLabel('Xác nhận mật khẩu mới',{exact:true}).fill(password+'different');
  await page.getByRole('button',{name:'Lưu mật khẩu mới'}).click();
  check(await page.getByText('Xác nhận mật khẩu chưa khớp.',{exact:true}).isVisible(),'Xác nhận sai hiển thị lỗi đúng');
  check(await page.getByLabel('Mật khẩu mới',{exact:true}).inputValue()==='' && await page.getByLabel('Xác nhận mật khẩu mới',{exact:true}).inputValue()==='','Không trả ngược mật khẩu đã nhập trong trang lỗi');
  const csrf=await context.request.post(webUrl+'/Account/ChangeTemporaryPassword',{form:{MatKhauMoi:password,XacNhanMatKhau:password}});
  check(csrf.status()===400,'Form đổi mật khẩu từ chối POST thiếu antiforgery token');
  await page.getByLabel('Mật khẩu mới',{exact:true}).fill(password);await page.getByLabel('Xác nhận mật khẩu mới',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Lưu mật khẩu mới'}).click();await page.waitForURL('**/Account/Login');
  check(true,'Đổi mật khẩu thành công yêu cầu đăng nhập lại');
  await page.getByLabel('Tên đăng nhập, email hoặc số điện thoại').fill(username);await page.getByLabel('Mật khẩu',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Đăng nhập',exact:true}).click();await page.waitForURL(webUrl+'/');
  await page.goto(webUrl+'/Account/Profile');
  check((await page.locator('body').innerText()).includes('Khách kiểm tra đổi mật khẩu tạm'),'Mật khẩu mới cho phép dùng hồ sơ thật');
  await context.request.post(apiUrl+'/api/auth/logout',{headers});
  console.log('WEB QA PASSED: '+passed+' checks.');
 } finally {await browser.close();}
})().catch(e=>{console.error(e.message);process.exitCode=1;});
