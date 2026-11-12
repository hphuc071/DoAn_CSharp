# Nguyên liệu — hai đơn vị cố định

## Yêu cầu đã áp dụng

- Chỉ chọn Gam (g) hoặc Mililit (ml); ID lấy từ API, không gán cứng 1/2.
- Bỏ nút và dialog quản lý Đơn vị tính, bỏ bảng quy đổi cùng các nút/ô hệ số trong form nguyên liệu.
- Form gọn: tên, đơn vị, tồn an toàn, ngưỡng cảnh báo, mô tả, đang sử dụng. Lưu/Hủy nằm ngoài vùng cuộn, căn theo nội dung. Bảng danh sách có cột đơn vị và số lượng dễ đọc.
- BOM tự lấy đơn vị theo nguyên liệu; không chọn chéo Gam/Mililit, không nhập đơn vị quy đổi khác.
- API chặn đơn vị ngoài g/ml và hệ số khác 1. Các route quản lý đơn vị cũ trả 409 với giải thích; giữ đường dẫn/DTO để client cũ không bị hiểu nhầm thao tác thành công.
- Nguyên liệu đã có BOM/tồn/giao dịch vẫn không được đổi đơn vị. Tạo/sửa/ngừng/xóa có điều kiện và kiểm tra version/audit được giữ nguyên.

Đây là điều chỉnh theo yêu cầu mới, thay thế phần CRUD đơn vị và nhập quy đổi trong UC-ADM-03 cũ; số UC và số bảng không đổi.

## Dữ liệu

Kiểm tra database thực tế trước sửa: hai đơn vị đang hoạt động (g/ml), 6 nguyên liệu dùng g, 3 dùng ml, không có quy đổi khác cơ sở. Không cần chuyển dữ liệu. Giữ nguyên 37 bảng, nguyên liệu, tồn kho, BOM và chứng từ. Hàng quy đổi cơ sở hệ số 1 vẫn được duy trì nội bộ để tương thích công thức/kho; người dùng không phải khai báo.

## Các file chính

| File | Trách nhiệm |
|---|---|
| Shared/Contracts/Admin/IngredientUnits.cs | Mã/nhãn cố định dùng chung |
| Desktop/Admin/Ingredients/IngredientEditorForm.cs | Form gọn, chỉ hai lựa chọn, giữ đúng đơn vị khi mở sửa |
| Desktop/Admin/Ingredients/IngredientsPage.cs | Danh sách/tìm kiếm/ba hành động, không còn quản lý đơn vị |
| Desktop/Shared/Controls/Admin/AdminWorkspacePage.cs | Dùng khung chuẩn cho trang nguyên liệu |
| Desktop/Shared/Controls/Admin/AdminListLayout.cs | Căn ô nhập và nhãn ngữ cảnh Kho/Nguyên liệu |
| Api/Features/Admin/Ingredients/IngredientService.cs | Chặn đơn vị/quy đổi ngoài chính sách, giữ bảo vệ tham chiếu |
| Api/Features/Admin/Recipes/RecipeService.cs | Kiểm tra định mức dùng đúng đơn vị cơ sở |
| Desktop/Admin/Recipes/RecipeEditorForm.cs | Đơn vị tự theo nguyên liệu |
| tests/FourChang.Tests/QuanLy/UC-ADM-03/IngredientAdministrationTests.cs | Test API đơn vị cố định, lỗi đầu vào, version và rollback |
| tests/FourChang.Tests/QuanLy/DungChung/AdminFeaturesUiTests.cs | Test lưu bằng cả hai đơn vị, mở sửa/BOM đúng đơn vị, nút lưu và ảnh |

Các đường dẫn Api/Desktop/Shared ở bảng là phần cuối trong src/Backend, src/Presentation, src/Shared tương ứng. Không tạo project mới, không thêm migration trong thay đổi này.

## Kết quả

| Bộ kiểm tra | Kết quả | TRX trong artifacts/tests/final |
|---|---|---|
| Nguyên liệu / BOM / tồn / giao diện | 20/20 đạt | `ingredient-fixed-units.trx` |
| Kiểm tra giao diện cuối | 6/6 đạt | `ingredient-fixed-units-ui-final.trx` |

Lượt UI cuối lặp một số test của bộ đầu, không cộng thành số test duy nhất. Đã xem trực quan ảnh danh sách, form desktop/compact và BOM. Các project liên quan build thành công, test dùng database GUID riêng. Backend local kiểm tra ready với 37 bảng.

## Mở giao diện mới

Ứng dụng đang mở vẫn giữ DLL cũ. Đóng cửa sổ Quản lý rồi chạy:

```powershell
.\Chay-QuanLy.cmd
```

Lệnh này đã được sửa để build lại project khi có .NET SDK, tránh mở lại giao diện cũ chỉ vì file exe đã tồn tại. Không tự đóng cửa sổ đang mở để tránh mất dữ liệu nháp. Chưa commit/push GitHub.
