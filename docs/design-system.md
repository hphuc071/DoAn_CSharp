# Thiết kế giao diện 4CHANG LINH NGU LAM

Tham chiếu hình người dùng cung cấp: nền kem, nâu cà phê, ảnh món, thẻ viền mảnh, cột tùy chỉnh bên phải, sidebar POS/Admin, KPI và biểu đồ cột. Điều chỉnh phép cộng mẫu để tổng đúng: POS 45.000 + 39.000 = 84.000; đưa 100.000 thì thừa 16.000. Không sao chép con số tổng 94.000 trong hình tham khảo.

| Thành phần | Quy ước |
|---|---|
| Primary | `#75452D` — nút chính, lựa chọn, thanh tiến trình |
| Sidebar | `#30241D` — chữ trắng/kem |
| Nền / surface | `#F7F2EA` / `#FFFDF9` |
| Viền / chữ phụ | `#E5DCD1` / `#81756A` |
| Trạng thái | Xanh nhạt xác minh/hoạt động, vàng chờ/đối soát, đỏ lỗi/hủy |
| Typography | Segoe UI thống nhất; Web fallback Tahoma/Arial; chữ đậm WinForms dùng FontStyle.Bold |
| Web | Lưới 3 món desktop, 2 món mobile; sidebar về dưới khi màn hình hẹp |
| Desktop | Sidebar trái; header + thanh trạng thái mẫu; nội dung cuộn, tab cho nghiệp vụ nhiều bước |
| Form | Nhãn rõ, đơn vị/số tiền có hậu tố; mật khẩu được che; lý do/chứng cứ đặt tại thao tác nhạy cảm |
| Bảng | Readonly để review; header kem, hàng xen kẽ, chọn dòng nâu nhạt; CRUD qua hộp thoại |
| Lỗi | Thông điệp rõ tình huống, dữ liệu nháp chưa được hiểu là đã lưu; mất kết nối không xác nhận giao dịch |

Nguồn token Web: `wwwroot/css/tokens.css`. Nguồn theme Desktop: `DesignSystem/CafeTheme.cs`. Hai nền tảng giữ cùng màu nhưng không cố chia sẻ control framework khác nhau.

## Tài sản hình ảnh

Ảnh món được tạo mới bằng công cụ ImageGen tích hợp, chế độ generate, nền đặc. Tệp `assets/images/coffee-sheet.png` kích thước 1536×1024, lưới 3 cột × 2 hàng. Không lấy ảnh bên ngoài và không chứa chữ/nhãn. Dùng CSS sprite trên Web và source rectangle trong WinForms, tránh 6 bản sao ảnh.

Prompt tạo tài sản: “Create a clean photorealistic product photography sprite sheet for a Vietnamese café UI. Exact 3-column by 2-row grid, six equal square cells, no gutters, no text, no labels, no borders, no logos, warm pale cream background. Top row: Vietnamese iced milk coffee in a clear glass; bac xiu creamy iced coffee in a tall glass; hot latte in a white ceramic cup with latte art and saucer. Bottom row: peach iced tea with peach slices in a clear glass; iced matcha latte in a clear glass; dark iced cold brew in a clear glass. One centered drink per cell, soft studio shadows, coherent warm café photography.”

Các ảnh `docs/previews` là ảnh chụp/render từ ứng dụng thật, không phải ảnh AI giả lập màn hình.
