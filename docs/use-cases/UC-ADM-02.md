# UC-ADM-02 — Quản lý thực đơn và công thức

Nguồn: `Dac-ta-8-UC-Quan-ly-Quy-tac-lien-quan.docx`, bản tinh gọn đã chốt ngày 15/11/2026. Thay nội dung ADM02 theo số cũ. Chỉ áp dụng **BR-03, BR-08, BR-11, BR-12, BR-13, BR-23, BR-24, BR-28, BR-29, BR-38**.

| Luồng | Triển khai trong file/thư mục cũ | Kiểm chứng |
|---|---|---|
| Chính 1–2: tìm, thêm/sửa món | MenuManagementPage có 4 tab Món/Cấu hình món/Topping/Công thức; MenuEditorForm và MenuAdminService | Loại món cố định, tên/ảnh/mô tả, giữ nháp khi lỗi; API kiểm quyền Admin |
| Chính 3: cấu hình giá | Size/nhiệt độ riêng, mặc định duy nhất thuộc món, giá VND chính xác | Chặn tổ hợp trùng, giá âm/lẻ, phiên bản cũ; menu và checkout cùng giá cấu hình |
| Chính 4–5: công thức | RecipeEditorForm/RecipeService: lưu áp dụng ngay, chọn nhiều nguyên liệu, nhập g/ml, FIXED/SUGAR/ICE và % đường mặc định | Rỗng, lượng/đơn vị sai, nguyên liệu ngừng không lưu; audit lỗi rollback |
| Chính 6/A2: hiển thị/bật bán | MenuAdminService và CatalogReadService | Tắt bán giữ thẻ Tạm hết; Ẩn loại thẻ. Bật bán cần mặc định/công thức hợp lệ, thiếu kho vẫn khóa mua |
| A1/A3: topping chung | Tab Topping; giá/phần, giới hạn/ly, công thức riêng | Topping đang dùng xuất hiện cho mọi cấu hình; không tự thêm vào giỏ, thiếu nguyên liệu không được đặt |
| A3/BR13: giữ lịch sử | Lưu sửa công thức tự thay bản hiện hành ở bên trong, giữ nguyên dòng công thức đã dùng; không sửa snapshot đơn | Checkout trước/sau sửa có công thức tương ứng, đơn cũ bất biến; sửa giá/topping không sửa giá đã chốt |
| E4: đồng thời, lỗi mạng/lịch sử | AdminOperation transaction, rowversion, receipt và outbox; form giữ payload khi chưa rõ kết quả | Replay/cạnh tranh cùng mã chỉ ghi một lần; stale payload bị chặn; receipt khôi phục sau mất phản hồi |

## Hướng dẫn thao tác

1. Món mới: thêm món ở trạng thái **ẩn**, thêm cấu hình size/nhiệt độ/giá (đặt một cấu hình mặc định).
2. Mở tab **Công thức**, thêm công thức cho đúng cấu hình và lưu. Hệ thống áp dụng ngay, không cần công bố phiên bản.
3. Bật cấu hình; mở món, chọn **Hiển thị món** và **Bật bán**. Mỗi cấu hình được bật cần công thức hợp lệ.
4. Topping mới lưu ngừng sử dụng trước, thêm công thức cho topping rồi kích hoạt ở tab Topping.

## Giới hạn phạm vi và tương thích

Không còn CRUD danh mục, banner, lịch công bố, cấu hình đường/đá hoặc topping riêng, xóa món/cấu hình/topping, clone/công bố/ngừng/xóa phiên bản hoặc mô phỏng công thức. Các route cũ tương ứng bị chặn sau kiểm quyền (410; CRUD loại món cố định trả409). Mức đường/đá chung ở CafeOptions; không phát sinh cấu hình nóng/lạnh chưa khai báo.

Giữ 30 bảng và toàn bộ dữ liệu lịch sử; bổ sung duy nhất CongThuc.PhanTramDuongMacDinh qua migration009 có sao lưu trước khi nâng cấp. Cột lịch/phiên bản và banner cũ giữ tương thích dữ liệu/Web; Admin không tạo lịch hoặc banner mới. Worker lịch cũ giữ để xử lý dữ liệu lịch sử nếu có. Công thức đã thay thế không hiện trong danh sách chỉnh sửa, vẫn còn trong DB/đơn.

BR03 dùng **khả dụng = tồn thực tế − giữ chỗ** thống nhất catalog, Web checkout, POS checkout và đọc tồn. Chưa bỏ cột/giao diện tồn an toàn của UC-ADM-03 cũ; cột đó không còn làm giảm khả dụng.

Test API/SQL và Browser ở `tests/FourChang.Tests/QuanLy/UC-ADM-02`; test công thức tái sử dụng `QuanLy/UC-ADM-02/RecipeAdministrationTests.cs` nhưng gắn Trait UC-ADM-02. Test WinForms tái sử dụng AdminFeaturesUiTests. Mọi test ghi trên DB GUID riêng; không xóa/reset dữ liệu FourChangDb.

## Form công thức dễ dùng — 15/11/2026

- Khi thêm chỉ chọn món/topping chưa có công thức. Nếu nhóm đã đầy, hướng dẫn đóng cửa sổ và dùng Sửa công thức; API vẫn kiểm trùng khi lưu.
- Tách thông tin công thức và phần nhập nguyên liệu; tên nóng/lạnh và cách tính lượng bằng tiếng Việt.
- Lượng cố định / Theo % đường / Theo % đá giữ các mã FIXED/SUGAR/ICE ở API. Không nhập hao hụt; dùng bao nhiêu nguyên liệu trừ đúng bấy nhiêu, theo lượng và lựa chọn đường/đá của từng ly.
- Ô lượng g/ml bỏ số0 thừa; 2,0000 hiện2, 2,5000 hiện2,5 theo định dạng Windows. NumericUpDown vẫn giữ4 chữ số lẻ; không sửa precision database. Helper Amount dùng chung nguyên liệu/công thức.
- Thêm/cập nhật nguyên liệu giữ chọn dòng vừa nhập, không trả ô nhập về dòng đầu. Nút lưu/hủy luôn nằm ngoài vùng cuộn.

## Chọn ảnh và sắp xếp tự động — 15/11/2026

Form món/topping có nút Chọn hình ảnh, Bỏ ảnh và xem trước; không nhập sprite hoặc đường dẫn thủ công. PNG/JPG tối đa5MB và4096×4096; Desktop kiểm ảnh thật, chuẩn hóa PNG và chỉ upload khi Lưu. API kiểm quyền Admin, dung lượng/kiểu/kích thước, lưu file nguyên tử theo SHA256; thử lại cùng ảnh trả cùng đường dẫn. Lưu menu mới ghi tham chiếu ảnh vào DB qua audit/receipt hiện có.

Ảnh lưu ở `artifacts/menu-images` bên dưới content root API (cấu hình MenuImages:Directory có thể đổi); độc lập thư mục DLL. Giữ thư mục ảnh khi sao lưu/chuyển máy cùng DB. Test dùng thư mục ảnh riêng theo fixture. GET /menu-images/{SHA256}.png chỉ phục vụ PNG công khai theo tên hợp lệ; Web proxy cùng origin; POS/Admin tái sử dụng CatalogImage. Không lưu đường dẫn C:/D: của máy chọn ảnh vào DB.

Bỏ trường Thứ tự ở form. Server cấp max(ThuTuHienThi)+1 khi thêm dưới khóa/transaction; sửa giữ vị trí hiện có, bỏ qua số thứ tự client cũ gửi. Không đánh lại thứ tự hoặc xóa dữ liệu cũ; menu vẫn sắp ThuTuHienThi rồi MaMon. Không thay schema.

### Ảnh WebP tải từ thiết bị

Bộ chọn hỗ trợ PNG/JPG/WebP và có Tất cả tệp. MenuEditorForm dùng SkiaSharp4.156.1 nhận dạng nội dung và đọc ảnh, kiểm kích thước trước giải mã; chuẩn hóa PNG rồi dùng nguyên API kho ảnh hiện có. Không chỉ đổi phần mở rộng .webp sang .png. File gốc của người dùng không thay đổi. Giữ giới hạn5MB/4096px và chỉ upload lúc Lưu.

## Món mới dùng công thức có sẵn — 15/11/2026

Form Thêm món có Dùng công thức từ món: chọn Tự tạo công thức sau hoặc chọn món mẫu có đủ cấu hình mặc định và công thức. Chọn mẫu lấy các cấu hình đang dùng (size/nhiệt độ, giá, mặc định), tạo công thức ACTIVE và sao chép từng nguyên liệu/loại lượng và % đường mặc định; bản sao mới không cộng hao hụt. Bản sao có mã riêng; sửa món mới không thay món nguồn hoặc snapshot đơn cũ. Chỉ áp dụng chọn món mẫu lúc thêm, không thay hàng loạt cấu hình của món đã tồn tại.

Món, cấu hình, công thức, lịch sử và sự kiện cùng transaction/khóa và receipt hiện có: nguồn sai hoặc lưu lịch sử lỗi hoàn tác toàn bộ; retry cùng mã không tạo lại. Không thêm bảng hoặc chia sẻ khóa công thức giữa các món. Cột mặc định đường được nâng cấp riêng qua migration009. Không kích hoạt lại route clone phiên bản cũ.

Tab Công thức hiển thị Món/topping, Size, Nóng/lạnh, Tên công thức, Số nguyên liệu và Tình trạng. Có tìm tên, lọc Món/Topping, Đã có/Chưa có. Cấu hình/topping chưa có công thức hiển thị dòng hướng dẫn; chọn dòng và Thêm/sửa theo dòng hoặc nhấp đúp mở form đúng đối tượng. Dòng chưa có chỉ là dữ liệu giao diện, không tạo công thức rỗng trong DB.

Form công thức cho chọn một công thức đang áp dụng rồi Lấy nguyên liệu, tự gợi ý tên theo đối tượng, sau đó chỉnh lượng, cách tính và % đường mặc định rồi Lưu. Nếu form đã nhập nguyên liệu thì hỏi trước khi thay nháp. Tất cả mức g/ml và phần trăm vẫn giữ chính xác như trước. Nguyên liệu mẫu đã ngừng không được đề xuất; server tiếp tục xác thực khi lưu.

Lối tắt Thêm size / nóng-lạnh trong tab Công thức dùng lại form cấu hình cũ. Sau khi lưu cấu hình, mở ngay form công thức đúng cấu hình mới; cấu hình đầu của món tự được chọn mặc định. Hủy nhập công thức để lại dòng Chưa có công thức, không tạo BOM rỗng. Bật bán cấu hình/món vẫn theo điều kiện công thức và kho hiện có.

## Nhập nhiều nguyên liệu và đường mặc định — hiện hành 15/11/2026

- Đánh dấu nhiều nguyên liệu, bấm **Thêm nguyên liệu đã chọn**, nhập lượng ngay trên từng dòng. Đơn vị g/ml theo nguyên liệu, không tự đổi. Có thể bỏ dòng; lượng phải dương, tối đa4 chữ số thập phân. Nhận cả dấu phẩy hoặc chấm thập phân; không hiện số0 thừa.
- Cách tính: **Lượng cố định**, **Theo % đường**, **Theo % đá**. Khai báo đường/đá ở mức100%; lúc đặt hệ thống nhân mức khách chọn và số ly. Ví dụ đường20g, khách chọn75%, 2ly: dùng30g đường.
- Mỗi công thức chọn sẵn đường0/25/50/75/100%; Web/POS dùng mức này khi chọn món mới, vẫn cho đổi. Sửa món trong giỏ giữ lựa chọn đã nhập. Món nóng không có % đá.
- Trừ đúng lượng nguyên liệu khi **bắt đầu pha**. **Pha xong** không trừ thêm; gửi lại thao tác không trừ hai lần. Không có bước hao hụt riêng. Cột TyLeHaoHut cũ và snapshot đơn cũ giữ để bảo toàn lịch sử; công thức mới/sửa lưu0, checkout mới không cộng tỷ lệ cũ.
- Sửa công thức áp dụng cho đơn mới; giữ nguyên định mức đã chốt của đơn cũ. Lấy nguyên liệu từ mẫu sao chép cả mức đường mặc định, không sửa nguồn.

## Bảng công thức theo món — thay luồng nhập từng cấu hình

Chọn món một lần; mỗi tab Lạnh/Nóng có các cột size đã khai báo. Chọn nhiều nguyên liệu và nhập lượng g/ml cho từng size; ô trống/0 là không dùng. Đường mặc định áp dụng các size trong lần lưu. Tab Nóng bỏ dòng theo % đá; nút Lấy lượng Lạnh sang Nóng sao chép cùng size rồi cho chỉnh riêng, hỏi trước khi thay nháp nóng đã nhập. Công thức nóng cũ được giữ khi mở, không tự ghi đè bằng lạnh.

Danh sách hiển thị một dòng mỗi món và báo còn size thiếu. Topping vẫn dùng form một phần. Không tự thêm size, giá hoặc đổi schema. Lưu tất cả size qua POST /api/admin/recipes/dish dùng chung SaveCore, một transaction/receipt/audit/outbox: một cấu hình lỗi hoàn tác toàn bộ, phiên bản cũ bị chặn, thử lại cùng mã không ghi đôi. BOM riêng từng cấu hình và snapshot đơn cũ vẫn giữ nguyên; trừ kho như trước.

## Thêm nhiều cấu hình một lần

Thêm cấu hình món và lối tắt Thêm size/nóng-lạnh mở MenuVariantBatchForm: chọn món, Nóng/Lạnh, tick các size, nhập giá theo cột và chọn một mặc định. Bỏ chọn size/nhiệt độ chỉ bỏ qua tạo mới, không xóa cấu hình cũ. Tổ hợp có sẵn nạp đúng giá và ID để sửa; giữ trạng thái bán hiện hành. Cấu hình mới lưu tắt bán. Lưu & nhập công thức mở bảng công thức của món vừa lưu; hủy nhập công thức giữ cấu hình nháp. Sửa từng cấu hình hiện có vẫn có form cũ để bật/tắt bán sau khi hoàn thiện công thức.

POST /api/admin/menu/variants/batch lưu một transaction/receipt/audit/outbox; kiểm đủ ID/version cấu hình cũ, không trùng tổ hợp, đúng một mặc định và giá VND nguyên. Lỗi một giá không lưu một phần; retry không tạo đôi. Không đổi bảng/cột hoặc lịch sử giá đơn cũ.

## Định lượng topping ngay trong form

Form Topping chọn nguyên liệu kho, nhập lượng mỗi phần (g/ml tự theo nguyên liệu), giá, giới hạn phần/ly và Đang sử dụng. MenuManagementPage nạp nguyên liệu/công thức vào form. Lưu tự tạo/sửa BOM FIXED một nguyên liệu trong cùng transaction topping, audit/receipt/outbox menu; lỗi lượng hoặc phiên bản công thức hoàn tác cả topping. SaveCore công thức được dùng lại nên giữ BOM/đơn cũ bất biến. DTO DinhLuong tùy chọn để client cũ giữ nguyên công thức. Công thức nhiều nguyên liệu cũ có lựa chọn Giữ định lượng hiện tại, không tự chuyển đổi dữ liệu quán. Không thay schema.

## Danh sách công thức chỉ dành cho món — 18/11/2026
- Tab Công thức không liệt kê topping, không có bộ lọc Món/Topping hoặc nút thêm size. Hai thao tác: Thêm công thức và Chỉnh sửa/nhấp đúp; cấu hình size vẫn ở tab Cấu hình món.
- Bảng gồm Tên món, Size, Nóng/lạnh, Nguyên liệu, Tình trạng. Size hiển thị đủ S/M/L đúng thứ tự; số nguyên liệu là số loại khác nhau trong toàn bộ size/nhiệt độ của món. Trạng thái phân biệt chưa có/chưa đủ/đã có công thức.
- Topping tiếp tục nhập nguyên liệu và lượng mỗi phần ở tab Topping; công thức nội bộ và lịch sử giữ nguyên để tính kho. Không đổi API, schema hoặc dữ liệu quán.

## Bỏ lựa chọn cách tính thủ công — 18/11/2026
Bảng công thức món chỉ hiện Nguyên liệu, Đơn vị và lượng từng size. Thêm Đường/Đá tự chọn cách tính theo phần trăm; nguyên liệu khác dùng lượng cố định. Công thức đã lưu giữ cách tính hiện có khi mở/sửa; không tự cập nhật database. Đường/đá khai báo lượng ở mức100%; bảng Nóng không dùng đá.
