# Tích hợp UC ADM 12 và ADM 13 cho nhóm

Phạm vi gồm hồ sơ nhân viên, quản lý ca và điểm nối Gửi đối chiếu tại POS đã được chấp thuận. Vẫn 26 UC, 35 bảng nghiệp vụ; không thêm migration, không thêm project hoặc dependency NuGet. Remote cấu hình hiện tại là `CSharp`, repository https://github.com/hphuc071/DoAn_CSharp.git. Các lệnh dưới đây để thành viên tự thực hiện; Codex chưa commit hoặc push.

## Phân công và điểm giao nhau

| Người | Phần sở hữu | Phần cần lấy đồng bộ |
|---|---|---|
| Thiện | API Features/Admin/Employees, Features/Admin/WorkSessions; Desktop Admin/Employees, Admin/WorkSessions | Contracts/Admin, WorkforceClient, Shared/WorkSessions; đăng ký Program/router |
| Phúc | Desktop Staff; API Features/Staff; POS-01/03/05 | Gửi đối chiếu, khóa ca, kiểm tra nhân viên ACTIVE, liên kết ca bàn giao, quy tắc thu/hoàn gắn ca |
| Tài và Thái | Web, Features/Customer | Shared contracts/client và các bản cập nhật solution; không gọi API Admin/POS hoặc nhập SQL trực tiếp |

Các file dễ xung đột: API/Program.cs (đăng ký service/endpoint), Contracts/Common/ScreenCatalog.cs, Desktop/Shared/Database/DatabaseViews.cs, tests/test-manifest.json. Khi merge giữ đăng ký của cả hai phía, không chọn toàn bộ “ours/theirs”. Sửa chức năng trong thư mục actor tương ứng; dùng các file partial API client riêng thay vì dồn hết vào CafeApiClient.cs.

## Hợp đồng nhóm cần thống nhất

1. API là nguồn dữ liệu chuẩn. Desktop/Web chỉ gọi HTTP; không tự tạo ca/nhân viên hoặc tự ghi số dư SQL.
2. Khóa nội bộ MaNhanVien là số và không tạo MaNhanVienHienThi hay tài khoản Staff. Tên có thể trùng.
3. Ghi hồ sơ bắt buộc thêm MaThaoTac GUID; sửa có PhienBanDuLieu; đổi trạng thái có LyDo. Giữ payload cũ sẽ bị 400: phải cập nhật Shared và API cùng phiên bản. GET danh sách/verify POS và OpenShift giữ nguyên payload cũ.
4. Chốt/bàn giao còn phải gửi `TienMatDuKienDaXem` từ chi tiết vừa đọc; máy chủ tính lại và trả 409 nếu nguồn tiền đã thay đổi dù rowversion ca chưa đổi.
5. Giữ cùng GUID và payload khi thử lại sau timeout. 409 yêu cầu đọc lại dữ liệu và người dùng xác nhận, không tự ghi đè. 401/403 không dùng phiên Staff để thay Admin.
6. UTC trên API/SQL, UI đổi giờ địa phương. Tiền decimal/VND nguyên, rowversion base64.
7. Phúc: mở ca theo API hiện có; gửi đối chiếu theo SubmitReconciliation. Sau gửi LOCKED, không thu/bán/hủy; Admin chốt hoặc cho tiếp tục/bàn giao. Đọc kết quả khóa bằng endpoint reconciliation có token ca cùng hai header thiết bị.
8. POS-05/ADM-10: khoản CASH thành công phải gắn MaCaLamViec thực hiện; thu chỉ VERIFIED và hoàn chỉ SUCCEEDED mới vào quỹ. CK không cộng tiền mặt. Ghi tiền/đơn phải kiểm tra lại trạng thái OPEN bên trong transaction (không chỉ dựa vào middleware trước đó). Dùng WorkforceOperation.Lock trong transaction trước đọc/ghi ca để không chạy đua với chốt/bàn giao.
9. POS-03: đơn bàn giao giữ MaCaLamViec nguồn, dùng HandoverLink.Ancestors để tìm đúng phạm vi ca được tiếp nhận; ghi giao dịch tiền mới vào ca hiện tại. Không chuyển khoản thu cũ hoặc đổi người của ca cũ.
10. Đơn dở/khoản chưa rõ phải xử lý tại UC sở hữu hoặc bàn giao có biên bản; ADM-13 không tự xác minh thanh toán hay giả hoàn tất đơn. DELIVERY_FAILED được coi là đơn kết thúc thất bại theo quyết định giữ database; không đổi mã SQL.
11. Không đổi bảng/khóa/cột để tiện riêng một actor. Mọi thay đổi contract/schema cần nhóm thống nhất trước.

## Đưa phần này lên GitHub

Thực hiện ở thư mục gốc dự án. Trước hết kiểm tra `git status`, nếu đang có sửa của thành viên khác thì commit/stash phần đó riêng, không trộn vào hai UC.

```powershell
git status
git switch -c thien/uc-adm-12-13
git diff --check
dotnet test FourChang.sln
git add src tests docs
git diff --cached --stat
git diff --cached --name-only
git commit -m "Implement ADM-12 employees and ADM-13 work sessions"
git fetch CSharp
git merge CSharp/main
dotnet test FourChang.sln
git push -u CSharp thien/uc-adm-12-13
```

Nếu có conflict: mở từng file, giữ cả đăng ký/routes hợp lệ, bỏ dấu conflict, `git add <file-da-sua>` rồi `git commit` để kết thúc merge. Không force push hoặc xóa phần của người khác. Sau push mở Pull Request từ nhánh này vào main, mô tả hai UC, hai quyết định đã chấp thuận và kết quả test. Chờ review rồi merge.

## Thành viên nhận thay đổi

Nếu remote của máy khác tên `origin`, thay `CSharp` trong lệnh bằng `origin` theo kết quả `git remote -v`.

```powershell
git status
git fetch CSharp
git switch main
git pull --ff-only CSharp main
git switch <nhanh-cua-thanh-vien>
git merge main
dotnet restore FourChang.sln
dotnet test FourChang.sln
```

Trước khi merge, lưu phần đang làm bằng commit trên nhánh của mình. Không chép đè thư mục src thủ công. Nếu cần nhận trước khi PR vào main, sau fetch dùng `git merge CSharp/thien/uc-adm-12-13` trên nhánh cá nhân.

## Cấu hình và chạy trên máy thành viên

Mỗi máy giữ .env riêng dựa trên .env.example; không commit mật khẩu/token/khóa POS. Không chia sẻ database test, bin, obj, artifacts hoặc tệp DPAPI. Khóa máy POS phải được Admin cấp trên từng máy.

```powershell
if (!(Test-Path .env)) { Copy-Item .env.example .env }
# Điền cấu hình riêng trong .env trước khi chạy tiếp.
docker compose up -d sqlserver
dotnet run --project src/Backend/FourChang.Api -- --init-db
dotnet test FourChang.sln
```

API và các giao diện mở bằng terminal riêng:

```powershell
dotnet run --project src/Backend/FourChang.Api --urls http://localhost:5181
dotnet run --project src/Presentation/FourChang.Admin
dotnet run --project src/Presentation/FourChang.Pos
dotnet run --project src/Presentation/FourChang.Web --urls http://localhost:5180
```

Kiểm thử nhanh hai UC: `dotnet test FourChang.sln --filter "UseCase=UC-ADM-12|UseCase=UC-ADM-13"`. Khi nộp PR vẫn chạy toàn bộ suite để kiểm tra các actor cùng hoạt động.

## Kiểm tra trước push

- Tên file, ProjectReference và namespace theo cấu trúc src mới; mở FourChang.sln.
- Kiểm tra danh sách staged không có .env, bí mật, dữ liệu khách hoặc artifacts. .gitignore đã loại .env và output; không dùng `git add -f` cho các file này.
- Không đánh dấu các UC xử lý đơn/thu/hoàn chưa triển khai là đã hoàn tất chỉ vì đã có điểm tích hợp.
- PR gồm code + contract + tests + docs liên quan trong cùng commit/nhánh để người khác nhận được bản tương thích.
