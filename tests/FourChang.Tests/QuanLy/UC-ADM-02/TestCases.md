# UC-ADM-02 — Thực đơn và công thức — Test cases

Nguồn: đặc tả 8 UC Quản lý tinh gọn. Quy tắc liên quan: **BR-03, BR-08, BR-11, BR-12, BR-13, BR-23, BR-24, BR-28, BR-29, BR-38**. Tái sử dụng project và file cũ.

| Mã TC | Tình huống và kết quả mong đợi | File/lớp cũ |
|---|---|---|
| 02-01 | Chỉ Admin được đọc/ghi menu/công thức; Staff/Customer403, vô danh401 | MenuAdministrationTests |
| 02-02 | Loại món cố định; thêm/sửa món ẩn, trùng tên/loại sai bị chặn; bản cũ409 | MenuAdministrationTests |
| 02-03 | Giá theo cấu hình, mặc định, đường/đá chung, topping chung; giá/giới hạn sai400 | MenuAdministrationTests |
| 02-04 | Không banner/lịch/xóa; tắt bán vẫn Tạm hết, ẩn loại thẻ, thiếu kho không nhận đơn | MenuAdministrationTests |
| 02-05 | Audit lỗi rollback menu/outbox; worker lỗi hoặc lease hết hạn phục hồi | MenuAdministrationTests |
| 02-06 | Mất phản hồi sau commit: tra receipt, gửi lại không ghi đôi; offline không lưu | MenuAdministrationTests |
| 02-07 | Sửa giá/topping không sửa snapshot dòng món cũ | MenuAdministrationTests |
| 02-08 | Lưu công thức áp dụng ngay; replay/concurrent cùng mã tạo một kết quả; giữ dòng bản cũ | RecipeAdministrationTests (thư mục UC-ADM-04 cũ) |
| 02-09 | Công thức rỗng, sai lượng/đơn vị/hao hụt, trùng dòng, nguyên liệu ngừng bị chặn | RecipeAdministrationTests |
| 02-10 | Checkout trước/sau sửa dùng đúng BOM, snapshot cũ bất biến; BR03 không trừ tồn an toàn | RecipeAdministrationTests |
| 02-11 | Audit công thức lỗi rollback; route clone/publish/stop/delete/simulate410 | RecipeAdministrationTests |
| 02-12 | Công thức topping mới áp dụng ngay, kích hoạt dùng chung mọi cấu hình | RecipeAdministrationTests |
| 02-13 | Món mới ẩn → cấu hình → công thức → bật/hiện, giá đúng, món nóng chỉ đá0 | RecipeAdministrationTests |
| 02-14 | Giá/khả dụng đồng bộ Web thật, giữ giỏ, ẩn/tắt đúng, mobile không tràn | MenuSynchronizationBrowserTests |
| 02-15 | Bốn tab, không nút đã bỏ, resize, form công thức giữ đúng đối tượng, lưu qua API thật | AdminFeaturesUiTests, route menu/recipes |

```powershell
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "UseCase=UC-ADM-02"
dotnet test tests/FourChang.Tests/FourChang.Tests.csproj --filter "FullyQualifiedName~GiaoDien_NamUseCase_DocApi_Resize_VaDialog"
```

Điều kiện: Windows/.NET8, SQL Docker và .env local hợp lệ, Playwright Chromium. Fixture tạo DB GUID riêng; ảnh và log trong artifacts/tests/xunit. Không dùng ảnh fixture để kết luận số khách hàng thật.

Bổ sung UI: chọn nhiều nguyên liệu g/ml, thêm vào bảng và sửa lượng trực tiếp; 2/2,5 không có0thừa, nhập2,5 lưu đúng2.5. Không còn ô hao hụt. Chọn mức đường mặc định75%, lưu và đọc lại đúng; API chặn tỷ lệ hao hụt khác0.

Bổ sung ảnh/thứ tự: MenuAdministrationTests kiểm401/403, sai base64/SVG/ảnh lớn, upload PNG thật, GET đúng byte/MIME/nosniff, replay không tạo file trùng, gắn ảnh vào menu; hai món tạo đồng thời có thứ tự kế tiếp và sửa giữ vị trí. MenuSynchronizationBrowserTests kiểm ảnh thật render/tải qua Web. AdminFeaturesUiTests route menu kiểm nút ảnh/xem trước, không còn ô thứ tự, lưu ảnh qua API thật trong fixture.

Test picker WebP: kiểm filter có *.webp, tạo WebP thật, chọn/xem trước đúng kích thước và Lưu qua API. Có thể đặt FOURCHANG_TEST_IMAGE_FILE để thử thêm ảnh tải về thực; dữ liệu vẫn ở fixture. menu-webp-final.trx đạt.

Bổ sung mẫu công thức: MenuAdministrationTests kiểm tạo món có đủ cấu hình/công thức riêng từ món mẫu, mặc định/giá/định mức đúng, gửi đồng thời cùng mã không trùng, sửa bản sao không thay nguồn; nguồn sai/audit lỗi rollback cả bốn bảng. UI menu chọn mẫu khi tạo món; UI recipes lọc dòng thiếu, mở đúng cấu hình, lấy nguyên liệu nguồn rồi lưu và dòng biến mất khỏi bộ lọc thiếu.

Bổ sung công thức15/11: RecipeAdministrationTests.NhieuNguyenLieu_DuongMacDinh_TruDungLuong_KhongHaoHut kiểm nhiều nguyên liệu, 2ly/đường75%, START trừ đúng một lần, FINISH không trừ thêm, mặc định0% lưu chính xác. Browser thật kiểm mặc định75% và đổi25%; POS kiểm mức mặc định theo API. Schema30MigrationTests kiểm database30 cũ có công thức nâng cấp giữ dữ liệu, nhận mặc định50%, chạy lại an toàn.

Kết quả: 31 test khác nhau đạt sau sửa. TRX trong tests/FourChang.Tests/TestResults: recipe-multi-sugar-direct-stock-v2.trx kết hợp các lần chạy lại recipe-defaults-stock-final.trx, recipe-web-sugar-migration-final.trx, recipe-multi-quantity-ui-final.trx. Các lỗi ở lượt đầu đã được kiểm lại thành công; không cộng lượt chạy lặp vào số test. Fixture dùng database riêng.

Bảng công thức theo món: BangSize_NongLanh_LuuNguyenTu_Replay_LoiKhongLuuDoDang kiểm tạo6 cấu hình, lượng riêng từng size, nóng không có ICE, đồng thời/replay một receipt, lỗi cột cuối rollback cả món, thiếu cấu hình/bản cũ bị chặn. Harness recipes kiểm bảng6 cột, nhập2,5, copy lạnh sang nóng, sửa nóng riêng, lưu6 công thức và danh sách chỉ một dòng/món, nút lưu khi thu nhỏ. Ảnh ADM02-matrix-cold/hot/compact.png trong fixture; không ghi dữ liệu quán thật.

Cấu hình nhiều size: CauHinhNhieuSize_Gia_MacDinh_Replay_Rollback kiểm3size lạnh, thêm3size nóng, đổi mặc định/giá, giữ ID/trạng thái cũ, đồng thời/replay, giá cuối sai rollback và snapshot cũ409. Harness menu kiểm ẩn/hiện cột nhiệt độ, mặc định M lạnh, nhập/lưu3size, mở lại thêm nóng không trùng, footer và tên nút đầy đủ khi thu nhỏ. Fixture riêng, ảnh ADM02-variant-batch.png và -compact.png.

Định lượng topping: Topping_DinhLuongTrongForm_LuuCungLuc_Replay_Kho kiểm tạo đang dùng cùng BOM30g, replay, sửa45g giữ dòng cũ, lượng0 rollback tên, version BOM cũ409 và đơn2ly×2phần giữ180g. Harness menu nhập nguyên liệu/lượng trong form thật, lưu rồi mở lại hiện30, không phải qua Recipes. Fixture riêng, ảnh ADM02-topping-portion.png.

- 18/11/2026: Danh sách công thức món tinh gọn (WinForms, route recipes): không hiện topping hoặc bộ lọc Món/Topping; còn 5 cột và 2 nút. Kiểm đủ S/M/L, số loại nguyên liệu không đếm trùng giữa size, trạng thái và mở/lưu bảng công thức; so sánh mã công thức topping trước/sau tải lại không đổi. Kết quả recipes-dishes-only.trx: 1/1 đạt, fixture SQL GUID riêng; ảnh ADM02-dish-recipes-clean.png.

- 18/11/2026: recipes-auto-calculation.trx1/1 đạt (WinForms/API/SQL fixture riêng): bảng S/M/L không còn ComboBox Cách tính; nhập/lưu/mở lại lượng lẻ ở cột size đầu; tự nhận Đường=SUGAR, Đá=ICE, Sữa=FIXED; nóng không đá; lưu6cấu hình và đường mặc định75%. Ảnh ADM02-auto-calculation.png.
