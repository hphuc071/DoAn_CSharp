# Tự động nhận diện và giao diện đăng nhập

| Tình huống | Mong đợi |
|---|---|
| Chưa nhập mã/tên | Không có nút Kiểm tra nhân viên, không hiện trạng thái chờ thừa |
| Nhập mã/tên rồi dừng gõ | Sau 500ms tự gửi xác minh; giữ quyền thiết bị hiện hành |
| Sửa mã hoặc tên | Ẩn kết quả cũ ngay, hủy request trước nếu còn chạy |
| Nhập sai rồi sửa nhanh | Kết quả cuối phải thuộc mã/tên mới, không ghi đè bởi phản hồi cũ |
| Mở ca | Kiểm tra lại phía client và API; không tin kết quả tự động đã lưu trước đó |
| Nhân viên trùng tên | Mã nhân viên vẫn phân biệt đúng hồ sơ |
| Logo | Web và Desktop dùng vòng tròn tách cà phê với bốn hạt; không dùng ảnh cốc cũ ở đăng nhập |
| Nút vào ứng dụng | Đăng nhập Admin, khách và mở ca chỉ có chữ, không có icon khóa |
| Đóng cửa sổ | Dừng timer, hủy request tự động; không giữ callback cập nhật giao diện đã đóng |

Chạy `dotnet test FourChang.sln --filter "UseCase=UC-POS-01"`. Test API giữ các tình huống nhận diện đã có trên database tạm. Kiểm thử Web trong AccountAccess bổ sung kiểm tra logo mới và ảnh desktop/mobile. Không thêm bảng hoặc tài khoản nhân viên.
