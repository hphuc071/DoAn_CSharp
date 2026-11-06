# Tích hợp ADM-03 cho nhóm

## Quyền sở hữu file

- Thiện: `Api/Features/Admin/Menu`, `Desktop/Admin/Menu`, entities/configurations/schema 37 và contracts/client dùng chung.
- Tài/Thái: `Web/Pages/Menu`, `_ProductCustomizer`, `wwwroot/js/catalog.js`, `Services/CatalogProxy.cs`; giữ DTO từ Shared.
- Phúc: `Desktop/Staff/Sales`, `Shared/Controls/CatalogImage.cs`; dùng GetCatalogAsync/WatchCatalogAsync từ Shared.
- Test chung: `tests/FourChang.Tests/QuanLy/UC-ADM-03`; không tạo thêm .csproj.

## Hợp đồng phải dùng chung

| API | Sử dụng |
|---|---|
| GET /api/catalog | Catalog đã công bố: mã danh mục/món/cấu hình/topping, giá, mặc định, lựa chọn hợp lệ, khả dụng, banner |
| GET /api/catalog/events | SSE chỉ báo `data: changed`; client tải lại snapshot, reconnect cũng tải lại |
| GET /api/menu | Hợp đồng danh sách cũ, giá của cấu hình mặc định |
| GET /api/menu/{MaMon}/variants | Cấu hình của món công khai; trả 404 cho nháp/ẩn/chưa đến lịch |
| GET /api/admin/menu | Snapshot quản trị có rowversion, chỉ Admin |
| POST/PUT /api/admin/menu/categories,dishes,variants,toppings,banners | Tạo/sửa với MaThaoTac, phiên bản cũ khi sửa |
| POST /api/admin/menu/{kind}/{id}/delete | Xóa có điều kiện với version và MaThaoTac |
| GET /api/admin/menu/operations/{MaThaoTac} | Tra kết quả đã commit sau timeout, theo đúng Admin |

Đường Web `/catalog/data` và `/catalog/events` là proxy cùng origin, không đưa URL/API token nội bộ vào trình duyệt. Không hard-code giá theo tên món, size hoặc topping. Khi sự kiện tới: giữ lựa chọn/ghi chú/từ khóa, đối chiếu lại ID, thông báo khi giá/tùy chọn đổi. Snapshot không phải giữ kho hoặc khóa giá của đơn chưa gửi.

## Cách đưa lên GitHub và nhận code

1. Tạo nhánh `feature/adm-03-menu` từ nhánh chung mới nhất. Commit cả schema/manifest/entities/contracts/API/UI/test/docs trong một PR để nhóm nhận cùng phiên bản; không commit .env, khóa POS, mật khẩu, token, .bak, bin/obj hoặc ảnh/log artifacts nhạy cảm.
2. Review riêng các file dễ xung đột: `Program.cs`, `CafeDbContext.cs`, `.csproj` Infrastructure/API, `DatabaseBootstrap.cs`, `DatabaseViews.cs`, test Program/manifest và CSS chung. Giữ registration của các thành viên, không chép đè cả file.
3. Sau merge, mỗi thành viên `git pull --rebase`, mở Docker, cấu hình .env riêng từ mẫu. Build solution, sao lưu database local rồi chạy API `--init-db` để nâng cấp đã kiểm tra; không chạy schema khởi tạo trực tiếp lên dữ liệu cũ.
4. Chạy `--check-db` (37 bảng/73 FK), `dotnet test FourChang.sln --filter "UseCase=UC-ADM-03|UseCase=DatabaseUpgrade"` và test UC mình phụ trách.
5. Mở Admin → Thực đơn, đổi giá cấu hình đang công bố; Web và POS đang mở phải nhận giá mới, mã vẫn giống nhau. Thử tắt món/ẩn danh mục, banner có lịch, ngắt rồi nối lại client. Không coi việc nhận SSE là tạo đơn hoặc xác nhận thanh toán.

Không push/merge tự động trong công việc này. Các file nguồn và migration phải được nhóm review cùng nhau.
