# Điện thoại khách hàng, cấp lại mật khẩu và tinh chỉnh Dashboard

## Kết quả

- Giữ tên/logo 4CHANG LINH NGU LAM, palette nâu–kem, sidebar/header và bốn vùng KPI/doanh thu/menu/kho-nhật ký.
- Điện thoại khách bắt buộc đủ 10 chữ số ASCII ở form Admin, Web, API và SQL NOT NULL + CHECK; email vẫn tùy chọn. Số người nhận cũng giới hạn 10 chữ số.
- Cấp lại mật khẩu theo yêu cầu khách: Khách hàng → chọn khách → Đặt lại mật khẩu → nhập mới/xác nhận/lý do → Cấp lại mật khẩu. Ghi audit, thu hồi phiên, buộc khách đổi mật khẩu tạm; không lưu mật khẩu rõ hoặc mật khẩu xác nhận.
- Dashboard lấy dữ liệu thật từ bốn API hiện có; chỉ cập nhật nội dung sau khi cả bốn lượt đọc thành công. Có nút Làm mới và nhãn thời điểm; mất mạng giữ dữ liệu cũ kèm lỗi. Không tạo chuỗi doanh thu giả hoặc đổi phép tính tiền.
- Bảng nguyên liệu hiển thị ký hiệu đơn vị/khả dụng/cảnh báo từ ADM-05. Nhật ký lấy mới nhất trước giới hạn 100 dòng. Cột vừa vùng chứa, tooltip giữ nội dung dài.
- Màu chọn dòng #F0DDC7 thống nhất, badge được tô nền và giới hạn trong ô; test pixel so sánh nền giữa các cột. Không thêm dark mode hoặc thay logo/màu chủ đạo.

## Database thật

Đã sao lưu và RESTORE VERIFYONLY trước migration 005. Bản sao lưu: `/var/opt/mssql/backup/FourChangDb_before_required_phone_20261110_122432.bak` trong container SQL. Giữ nguyên 37 bảng và dữ liệu 20 khách; không tự thêm số hay xóa dữ liệu. Model/mapping/manifest kiểm tra cùng NOT NULL/CHECK/index.

## Kiểm thử

Build solution: 0 lỗi, 0 cảnh báo. Các lượt kiểm tra có thể trùng case; không cộng thành số test duy nhất.

| Bộ | Kết quả | TRX trong artifacts/tests/final |
|---|---|---|
| Hồi quy tổng hợp | 32/32 đạt | `customer-ui-refresh.trx` |
| Lượt UI cuối | 8/8 đạt | `customer-dashboard-ui-final.trx` |

Đã xem ảnh Dashboard desktop/compact, form cấp lại mật khẩu và bảng chọn dòng sau khi sửa. Fixture dùng database GUID, không chạy reset/xóa dữ liệu thật.

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-01|FullyQualifiedName~AdminFeaturesUiTests|FullyQualifiedName~MenuSynchronizationBrowserTests|FullyQualifiedName~DatabaseTests"
```

## Đồng bộ và mở bản mới

Backend, Web và Admin cùng dùng ràng buộc/contract/dữ liệu chung. Luồng reset đã kiểm tra qua form → API → phiên khách; đồng bộ menu Web được kiểm thử lại. POS tiếp tục đọc nguồn catalog API hiện có. Đây không phải xác nhận mọi UC của nhóm đã hoàn tất hoặc mọi nhánh đã merge.

Chưa commit/push lên GitHub, chưa tự merge code các thành viên. File .env vẫn được Git bỏ qua. Đồng đội cần pull/merge cả code, Shared, manifest và migration, sao lưu rồi chạy --init-db/--check-db; không chỉ chép file giao diện. Máy có khách thiếu số sẽ dừng nâng cấp để chủ dữ liệu quyết định.

Đóng bản Admin cũ, rồi từ thư mục đồ án chạy:

```powershell
.\Chay-QuanLy.cmd
```

Các file chính: CustomerRules.cs; CustomerEditorForm.cs; DashboardPage.cs; MenuListLayout.cs; CafeTheme.cs/CafeVisuals.cs/Ui.cs; DataEndpoints.cs (thứ tự đọc log); Account/Register/Profile trên Web; DatabaseBootstrap và cấu hình TaiKhoanKhachHang; migration/model 37 bảng; fixture và test UC-ADM-01, test menu browser, worker UI dùng chung. Không tạo project test mới.
