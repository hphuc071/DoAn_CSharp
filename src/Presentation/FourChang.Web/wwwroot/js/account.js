// Chỉ giữ nháp ngay trên form; không lưu thông tin cá nhân hay mật khẩu vào localStorage.
// Cảnh báo rời trang giúp khách không vô tình bỏ thay đổi chưa gửi (UC-WEB-02/03).
let accountDirty=false;
// Khi quay lại từ bộ nhớ lịch sử trình duyệt, xác thực lại thay vì hiện hồ sơ của phiên đã logout.
window.addEventListener('pageshow',event=>{if(event.persisted)location.reload();});
document.querySelectorAll('[data-account-draft]').forEach(form=>{
 form.addEventListener('input',()=>accountDirty=true);
 form.addEventListener('submit',event=>{
  if(!navigator.onLine){event.preventDefault();alert('Đang mất mạng. Giữ nguyên nháp và kết nối lại trước khi lưu.');return;}
  accountDirty=false;
 });
});
window.addEventListener('beforeunload',event=>{if(accountDirty){event.preventDefault();event.returnValue='';}});
document.querySelectorAll('[data-confirm-remove]').forEach(form=>form.addEventListener('submit',event=>{
 if(!confirm('Bỏ địa chỉ này khỏi sổ địa chỉ? Địa chỉ của đơn đã gửi vẫn được giữ nguyên.'))event.preventDefault();
}));
