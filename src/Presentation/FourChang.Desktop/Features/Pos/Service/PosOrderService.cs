using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourChang.Desktop.Features.Pos.Models;

namespace FourChang.Desktop.Features.Pos.Services
{
    // Giao diện truy xuất thực đơn và công thức
    public interface IMenuRepository
    {
        Task<MonDetail?> GetMonByIdAsync(string maMon);
        Task<ToppingItem?> GetToppingByIdAsync(string maTopping);
        Task<RecipeDetail?> GetRecipeByVariantAsync(string maMon, string size, bool isHot);
        Task<RecipeDetail?> GetRecipeByToppingAsync(string maTopping);
    }

    // Chi tiết món ăn và cấu hình kích cỡ
    public class MonDetail
    {
        public string MaMon { get; set; } = null!;
        public string TenMon { get; set; } = null!;
        public bool IsPublished { get; set; }
        public List<MenuItemOption> Sizes { get; set; } = new();
    }

    // Giao diện lưu trữ và truy xuất đơn hàng
    public interface IOrderRepository
    {
        Task<PosOrder?> GetByIdAsync(string maDonHang);
        Task CreateOrderAsync(PosOrder order);
        Task UpdateOrderAsync(PosOrder order);
        Task<List<PosOrder>> GetQueueAsync();
    }

    // Giao diện xử lý tồn kho và giữ chỗ
    public interface IInventoryService
    {
        Task<StockItem?> GetStockAsync(string maNguyenLieu);
        Task<bool> CheckAvailabilityAsync(Dictionary<string, decimal> requiredIngredients);
        Task<bool> ReserveInventoryForOrderAsync(PosOrder order);
        Task ReleaseReservationAsync(string maDonHang, List<OrderLineIngredientSnapshot> ingredientsToRelease);
        Task ConsumeReservedInventoryAsync(string maDonHang, string maDong, Dictionary<string, decimal> ingredientsToConsume);
    }

    // Giao diện ghi nhận và tra cứu thanh toán
    public interface IPaymentService
    {
        Task<PosPaymentRecord?> GetPaymentByOperationAsync(string maThaoTac);
        Task<PosPaymentRecord> RecordPaymentAsync(PosPaymentRecord payment);
    }

    // Dịch vụ xử lý nghiệp vụ đơn hàng POS
    public class PosOrderService
    {
        private readonly IMenuRepository _menuRepo;
        private readonly IWorkSessionRepository _sessionRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IInventoryService _inventoryService;
        private readonly IPaymentService? _paymentService;

        public PosOrderService(
            IMenuRepository menuRepo,
            IWorkSessionRepository sessionRepo,
            IOrderRepository orderRepo,
            IInventoryService inventoryService,
            IPaymentService? paymentService = null)
        {
            _menuRepo = menuRepo;
            _sessionRepo = sessionRepo;
            _orderRepo = orderRepo;
            _inventoryService = inventoryService;
            _paymentService = paymentService;
        }

        // Cấu hình dòng món và tính toán snapshot định mức BOM
        public async Task<OrderLineItem> BuildAndValidateLineItemAsync(ConfigureItemRequest req)
        {
            // Kiểm tra trạng thái xuất bản của món
            var mon = await _menuRepo.GetMonByIdAsync(req.MaMon);
            if (mon == null || !mon.IsPublished)
            {
                throw new InvalidOperationException($"Món {req.MaMon} không tồn tại hoặc đã ngừng bán.");
            }

            // Kiểm tra kích cỡ hợp lệ
            var sizeConfig = mon.Sizes.FirstOrDefault(s => s.Size == req.Size);
            if (sizeConfig == null)
            {
                throw new InvalidOperationException($"Size {req.Size} không được hỗ trợ cho món này.");
            }

            // Kiểm tra tùy chọn nóng lạnh và mức đá
            if (req.IsHot && !sizeConfig.IsHotAllowed)
            {
                throw new InvalidOperationException("Món không hỗ trợ tùy chọn uống Nóng.");
            }
            if (req.IsHot && req.MucDaPercent > 0)
            {
                throw new ArgumentException("Món uống nóng không được chọn mức đá.");
            }

            // Lấy công thức pha chế của biến thể món
            var variantRecipe = await _menuRepo.GetRecipeByVariantAsync(req.MaMon, req.Size, req.IsHot);
            if (variantRecipe == null)
            {
                throw new InvalidOperationException($"Không tìm thấy công thức pha chế hợp lệ cho món {mon.TenMon} ({req.Size}).");
            }

            var ingredientsMap = new Dictionary<string, OrderLineIngredientSnapshot>();

            // Tính định mức nguyên liệu theo tỷ lệ đường và đá
            foreach (var ing in variantRecipe.Ingredients)
            {
                decimal factor = 1m;
                if (ing.LoaiDinhMuc == "SUGAR")
                {
                    factor = req.MucDuongPercent / 100m;
                }
                else if (ing.LoaiDinhMuc == "ICE")
                {
                    factor = req.MucDaPercent / 100m;
                }

                decimal denominator = 1m - ing.TyLeHaoHut;
                if (denominator <= 0m) denominator = 1m;

                decimal dinhMucMoiLy = Math.Round((ing.DinhMucCoSo * factor) / denominator, 4);

                if (!ingredientsMap.ContainsKey(ing.MaNguyenLieu))
                {
                    ingredientsMap[ing.MaNguyenLieu] = new OrderLineIngredientSnapshot
                    {
                        MaNguyenLieu = ing.MaNguyenLieu,
                        TenNguyenLieu = ing.TenNguyenLieu,
                        DonViCoSo = ing.DonViCoSo,
                        DinhMucMoiLy = dinhMucMoiLy,
                        TongLuongDinhMuc = dinhMucMoiLy * req.SoLy
                    };
                }
                else
                {
                    ingredientsMap[ing.MaNguyenLieu].DinhMucMoiLy += dinhMucMoiLy;
                    ingredientsMap[ing.MaNguyenLieu].TongLuongDinhMuc += dinhMucMoiLy * req.SoLy;
                }
            }

            // Xử lý và tính giá các topping đính kèm
            decimal tongGiaTopping = 0m;
            var listToppingSnapshot = new List<OrderLineTopping>();

            foreach (var tSelect in req.Toppings)
            {
                var toppingInfo = await _menuRepo.GetToppingByIdAsync(tSelect.MaTopping);
                if (toppingInfo == null)
                {
                    throw new InvalidOperationException($"Topping {tSelect.MaTopping} không tồn tại.");
                }
                if (tSelect.SoPhan > toppingInfo.GioiHanSoPhan)
                {
                    throw new ArgumentException($"Topping {toppingInfo.TenTopping} vượt quá số phần cho phép ({toppingInfo.GioiHanSoPhan}).");
                }

                decimal giaPhanTopping = toppingInfo.GiaMoiPhan * tSelect.SoPhan;
                tongGiaTopping += giaPhanTopping;

                var toppingRecipe = await _menuRepo.GetRecipeByToppingAsync(tSelect.MaTopping);
                string? toppingRecipeCode = toppingRecipe?.MaCongThuc;

                // Cộng định mức nguyên liệu từ công thức topping
                if (toppingRecipe != null)
                {
                    foreach (var tIng in toppingRecipe.Ingredients)
                    {
                        decimal tDenominator = 1m - tIng.TyLeHaoHut;
                        if (tDenominator <= 0m) tDenominator = 1m;

                        decimal topDinhMucMoiLy = Math.Round((tIng.DinhMucCoSo * tSelect.SoPhan) / tDenominator, 4);

                        if (!ingredientsMap.ContainsKey(tIng.MaNguyenLieu))
                        {
                            ingredientsMap[tIng.MaNguyenLieu] = new OrderLineIngredientSnapshot
                            {
                                MaNguyenLieu = tIng.MaNguyenLieu,
                                TenNguyenLieu = tIng.TenNguyenLieu,
                                DonViCoSo = tIng.DonViCoSo,
                                DinhMucMoiLy = topDinhMucMoiLy,
                                TongLuongDinhMuc = topDinhMucMoiLy * req.SoLy
                            };
                        }
                        else
                        {
                            ingredientsMap[tIng.MaNguyenLieu].DinhMucMoiLy += topDinhMucMoiLy;
                            ingredientsMap[tIng.MaNguyenLieu].TongLuongDinhMuc += topDinhMucMoiLy * req.SoLy;
                        }
                    }
                }

                listToppingSnapshot.Add(new OrderLineTopping
                {
                    MaTopping = toppingInfo.MaTopping,
                    TenToppingSnapshot = toppingInfo.TenTopping,
                    SoPhanTrenLy = tSelect.SoPhan,
                    GiaMoiPhan = toppingInfo.GiaMoiPhan,
                    MaCongThucSnapshot = toppingRecipeCode
                });
            }

            decimal donGiaLy = sizeConfig.BasePrice + tongGiaTopping;
            decimal thanhTienDong = donGiaLy * req.SoLy;

            return new OrderLineItem
            {
                MaMon = mon.MaMon,
                TenMonSnapshot = mon.TenMon,
                Size = req.Size,
                IsHot = req.IsHot,
                MucDuongPercent = req.MucDuongPercent,
                MucDaPercent = req.MucDaPercent,
                Toppings = listToppingSnapshot,
                IngredientsSnapshot = ingredientsMap.Values.ToList(),
                MaCongThucSnapshot = variantRecipe.MaCongThuc,
                SoLy = req.SoLy,
                SoLyBatDauPha = 0,
                SoLyPhaXong = 0,
                SoLyDaHuy = 0,
                DonGiaLy = donGiaLy,
                ThanhTienDong = thanhTienDong,
                GhiChu = req.GhiChu,
                Status = "ACCEPTED"
            };
        }

        // Tạo đơn hàng nguồn POS tại quầy
        public async Task<PosOrder> CreatePosOrderAsync(CreatePosOrderRequest request)
        {
            // Kiểm tra trạng thái ca làm việc phải là OPEN
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            // Kiểm tra thông tin bắt buộc theo hình thức phục vụ
            if (request.FulfillmentType == "AT_TABLE" && string.IsNullOrWhiteSpace(request.SoBan))
            {
                throw new ArgumentException("Đơn tại bàn bắt buộc phải chỉ định số bàn.");
            }
            if (request.FulfillmentType == "TAKEAWAY" && string.IsNullOrWhiteSpace(request.TenNguoiNhan))
            {
                throw new ArgumentException("Đơn mang đi bắt buộc phải có thông tin người nhận.");
            }
            if (request.FulfillmentType == "DELIVERY")
            {
                if (string.IsNullOrWhiteSpace(request.DiaChiGiaoDaChot))
                {
                    throw new ArgumentException("Đơn giao tận nơi bắt buộc phải có địa chỉ nhận.");
                }
                if (request.KhoangCachKm is null or <= 0m)
                {
                    throw new ArgumentException("Đơn giao tận nơi bắt buộc phải có khoảng cách hợp lệ.");
                }
            }

            var order = new PosOrder
            {
                MaCaLamViec = session.MaCaLamViec,
                MaNhanVien = session.MaNhanVien,
                FulfillmentType = request.FulfillmentType,
                SoBan = request.SoBan,
                TenNguoiNhan = request.TenNguoiNhan,
                DienThoaiNguoiNhan = request.DienThoaiNguoiNhan,
                DiaChiGiaoDaChot = request.DiaChiGiaoDaChot,
                KhoangCachKm = request.KhoangCachKm,
                PhiGiaoHang = request.PhiGiaoHang,
                Source = "POS",
                OrderStatus = "PENDING_CONFIRMATION",
                TrangThaiThanhToan = "UNPAID",
                CreatedAt = DateTime.UtcNow
            };

            decimal tongTien = 0m;
            var totalRequired = new Dictionary<string, decimal>();

            // Xây dựng dòng món và tổng hợp nguyên liệu toàn đơn
            foreach (var itemReq in request.Items)
            {
                var lineItem = await BuildAndValidateLineItemAsync(itemReq);
                order.LineItems.Add(lineItem);
                tongTien += lineItem.ThanhTienDong;

                foreach (var ing in lineItem.IngredientsSnapshot)
                {
                    totalRequired[ing.MaNguyenLieu] = totalRequired.GetValueOrDefault(ing.MaNguyenLieu) + ing.TongLuongDinhMuc;
                }
            }

            order.TongTienHang = tongTien;
            order.TongThanhToan = tongTien + order.PhiGiaoHang;

            // Kiểm tra tồn kho khả dụng cho toàn bộ đơn hàng
            bool isAvailable = await _inventoryService.CheckAvailabilityAsync(totalRequired);
            if (!isAvailable)
            {
                throw new InvalidOperationException("Kho không đủ nguyên liệu khả dụng để đáp ứng toàn bộ đơn hàng.");
            }

            // Thực hiện giữ chỗ nguyên liệu trong kho
            bool reserveSuccess = await _inventoryService.ReserveInventoryForOrderAsync(order);
            if (!reserveSuccess)
            {
                throw new InvalidOperationException("Không thể giữ chỗ nguyên liệu cho đơn hàng.");
            }

            await _orderRepo.CreateOrderAsync(order);
            return order;
        }

        // Tiếp nhận đơn hàng từ hàng đợi
        public async Task<PosOrder> AcceptOrderAsync(AcceptOrderRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            if (order.OrderStatus != "PENDING_CONFIRMATION")
            {
                throw new InvalidOperationException("Chỉ đơn ở trạng thái Chờ xác nhận mới được tiếp nhận.");
            }

            // Kiểm tra ràng buộc thanh toán cho đơn hàng nguồn Web
            if (order.Source == "WEB" && order.TrangThaiThanhToan != "PAID")
            {
                throw new InvalidOperationException("Đơn hàng Web chưa thanh toán đủ tiền, không được phép tiếp nhận.");
            }

            // Chuyển trạng thái đơn sang ACCEPTED
            order.OrderStatus = "ACCEPTED";
            order.AcceptedAt = DateTime.UtcNow;
            order.Version++;

            foreach (var line in order.LineItems)
            {
                line.Status = "ACCEPTED";
            }

            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Từ chối đơn hàng chưa pha chế
        public async Task<PosOrder> RejectOrderAsync(RejectOrderRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(request.LyDo))
            {
                throw new ArgumentException("Bắt buộc phải nhập lý do từ chối đơn hàng.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            // Kiểm tra điều kiện chưa có dòng nào bắt đầu pha
            if (order.LineItems.Any(l => l.SoLyBatDauPha > 0))
            {
                throw new InvalidOperationException("Đơn hàng đã bắt đầu pha chế, không thể từ chối toàn đơn.");
            }

            // Giải phóng toàn bộ nguyên liệu đã giữ chỗ
            var allIngredients = order.LineItems.SelectMany(l => l.IngredientsSnapshot).ToList();
            await _inventoryService.ReleaseReservationAsync(order.MaDonHang, allIngredients);

            order.OrderStatus = "REJECTED";
            order.LyDoTuChoiHoacHuy = request.LyDo;
            order.Version++;

            // Ghi nhận nghĩa vụ hoàn tiền nếu đã thu trước
            if (order.SoTienDaThanhToan > 0m)
            {
                order.KhoanPhaiHoan = order.SoTienDaThanhToan;
            }

            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Bắt đầu pha chế từng phần số ly
        public async Task<PosOrder> StartPreparingLineItemAsync(StartLinePrepRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            var line = order.LineItems.FirstOrDefault(l => l.MaDong == request.MaDong);
            if (line == null)
            {
                throw new InvalidOperationException("Không tìm thấy dòng món trong đơn hàng.");
            }

            // Kiểm tra số ly bắt đầu không vượt quá phần chưa pha
            if (request.SoLyBatDau <= 0 || request.SoLyBatDau > line.PhanChuaPha)
            {
                throw new ArgumentException($"Số ly bắt đầu pha ({request.SoLyBatDau}) không hợp lệ (còn {line.PhanChuaPha} ly chưa pha).");
            }

            // Tính lượng nguyên liệu tiêu hao cho số ly bắt đầu pha
            var consumeDict = new Dictionary<string, decimal>();
            foreach (var ing in line.IngredientsSnapshot)
            {
                decimal amount = ing.DinhMucMoiLy * request.SoLyBatDau;
                consumeDict[ing.MaNguyenLieu] = amount;
            }

            // Trừ tồn thực tế và giảm giữ chỗ đúng một lần
            await _inventoryService.ConsumeReservedInventoryAsync(order.MaDonHang, line.MaDong, consumeDict);

            line.SoLyBatDauPha += request.SoLyBatDau;
            line.Status = "PREPARING";
            order.OrderStatus = "PREPARING";
            order.Version++;

            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Cập nhật số ly pha xong và sẵn sàng
        public async Task<PosOrder> MarkLineReadyAsync(MarkLineReadyRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            var line = order.LineItems.FirstOrDefault(l => l.MaDong == request.MaDong);
            if (line == null)
            {
                throw new InvalidOperationException("Không tìm thấy dòng món trong đơn hàng.");
            }

            // Kiểm tra số ly pha xong không vượt số ly đang pha
            if (request.SoLyPhaXong <= 0 || request.SoLyPhaXong > line.PhanDangPha)
            {
                throw new ArgumentException($"Số ly pha xong ({request.SoLyPhaXong}) không được vượt quá số ly đang pha ({line.PhanDangPha}).");
            }

            line.SoLyPhaXong += request.SoLyPhaXong;
            if (line.IsLineReady)
            {
                line.Status = "READY";
            }

            // Chuyển trạng thái đơn sang READY khi toàn bộ dòng hiệu lực xong
            bool allLinesReady = order.LineItems.All(l => l.IsLineReady || (l.SoLy == l.SoLyDaHuy));
            if (allLinesReady)
            {
                order.OrderStatus = "READY";
            }

            order.Version++;
            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Bàn giao món cho khách tại bàn hoặc mang đi
        public async Task<PosOrder> ServeOrderAsync(ServeOrderRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            // Kiểm tra điều kiện các dòng món hiệu lực đã sẵn sàng
            bool allReady = order.LineItems.All(l => l.IsLineReady || (l.SoLy == l.SoLyDaHuy));
            if (!allReady)
            {
                throw new InvalidOperationException("Đơn hàng chưa hoàn tất pha chế toàn bộ các dòng món.");
            }

            // Chặn bàn giao nếu chưa thu đủ tiền
            if (order.SoDuConThu > 0m)
            {
                throw new InvalidOperationException($"Đơn hàng chưa thu đủ tiền (còn thiếu {order.SoDuConThu:N0}đ). Vui lòng hoàn tất thanh toán trước khi bàn giao.");
            }

            // Chuyển trạng thái bàn giao theo hình thức phục vụ
            if (order.FulfillmentType == "DELIVERY")
            {
                order.OrderStatus = "DELIVERING";
            }
            else
            {
                order.OrderStatus = "SERVED";
            }

            order.Version++;
            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Hoàn tất đơn hàng và ghi nhận doanh thu
        public async Task<PosOrder> CompleteOrderAsync(CompleteOrderRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            // Kiểm tra điều kiện đã bàn giao và thu đủ tiền
            if (order.OrderStatus != "SERVED" && order.OrderStatus != "DELIVERING")
            {
                throw new InvalidOperationException("Chỉ đơn hàng đã bàn giao hoặc đang giao mới được hoàn tất.");
            }
            if (order.SoDuConThu > 0m)
            {
                throw new InvalidOperationException("Đơn hàng chưa thu đủ tiền, không thể hoàn tất.");
            }

            order.OrderStatus = "COMPLETED";
            order.CompletedAt = DateTime.UtcNow;
            order.Version++;

            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Hủy một phần số ly món chưa pha chế
        public async Task<PosOrder> CancelLineItemBeforePrepAsync(CancelLineItemRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(request.LyDo))
            {
                throw new ArgumentException("Bắt buộc phải nhập lý do hủy món.");
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            var line = order.LineItems.FirstOrDefault(l => l.MaDong == request.MaDong);
            if (line == null)
            {
                throw new InvalidOperationException("Không tìm thấy dòng món trong đơn hàng.");
            }

            // Chỉ cho phép hủy số ly chưa bắt đầu pha
            if (request.SoLyHuy <= 0 || request.SoLyHuy > line.PhanChuaPha)
            {
                throw new ArgumentException($"Số ly hủy ({request.SoLyHuy}) vượt quá số ly chưa pha ({line.PhanChuaPha}).");
            }

            // Giải phóng giữ chỗ nguyên liệu tương ứng với phần bị hủy
            var releaseList = new List<OrderLineIngredientSnapshot>();
            foreach (var ing in line.IngredientsSnapshot)
            {
                releaseList.Add(new OrderLineIngredientSnapshot
                {
                    MaNguyenLieu = ing.MaNguyenLieu,
                    TenNguyenLieu = ing.TenNguyenLieu,
                    DonViCoSo = ing.DonViCoSo,
                    DinhMucMoiLy = ing.DinhMucMoiLy,
                    TongLuongDinhMuc = ing.DinhMucMoiLy * request.SoLyHuy
                });
            }

            await _inventoryService.ReleaseReservationAsync(order.MaDonHang, releaseList);

            line.SoLyDaHuy += request.SoLyHuy;
            if (line.SoLyDaHuy == line.SoLy)
            {
                line.Status = "CANCELLED";
            }

            // Tính lại nghĩa vụ thanh toán của đơn
            decimal giamTien = line.DonGiaLy * request.SoLyHuy;
            order.TongTienHang -= giamTien;
            order.TongThanhToan -= giamTien;

            // Tính khoản phải hoàn nếu đơn đã thanh toán vượt nghĩa vụ
            if (order.SoTienDaThanhToan > order.TongThanhToan)
            {
                order.KhoanPhaiHoan = order.SoTienDaThanhToan - order.TongThanhToan;
            }

            // Nếu toàn bộ các dòng đều bị hủy thì hủy toàn đơn
            bool allCancelled = order.LineItems.All(l => l.SoLy == l.SoLyDaHuy);
            if (allCancelled)
            {
                order.OrderStatus = "CANCELLED";
            }

            order.Version++;
            await _orderRepo.UpdateOrderAsync(order);
            return order;
        }

        // Xử lý thanh toán cho đơn hàng tại quầy
        public async Task<PosPaymentRecord> ProcessPaymentAsync(ProcessPaymentRequest request)
        {
            // Kiểm tra phiên ca hợp lệ và đang mở
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc đã đóng hoặc không hợp lệ.");
            }

            // Chống trùng lặp giao dịch theo mã thao tác
            if (_paymentService != null && !string.IsNullOrWhiteSpace(request.MaThaoTac))
            {
                var existingPayment = await _paymentService.GetPaymentByOperationAsync(request.MaThaoTac);
                if (existingPayment != null)
                {
                    return existingPayment;
                }
            }

            var order = await _orderRepo.GetByIdAsync(request.MaDonHang);
            if (order == null)
            {
                throw new InvalidOperationException($"Đơn hàng {request.MaDonHang} không tồn tại.");
            }

            // Chặn thu tiền mặt đối với đơn nguồn Web
            if (order.Source == "WEB" && request.PhuongThuc == "CASH")
            {
                throw new InvalidOperationException("Đơn hàng Web không áp dụng thu tiền mặt tại quầy.");
            }

            // Không thu lại đơn Web đã thanh toán đủ
            if (order.Source == "WEB" && order.TrangThaiThanhToan == "PAID")
            {
                throw new InvalidOperationException("Đơn hàng Web đã thanh toán đủ tiền, không thu lại.");
            }

            decimal soTienCanThu = order.SoDuConThu;
            if (soTienCanThu <= 0m)
            {
                throw new InvalidOperationException("Đơn hàng đã được thanh toán đầy đủ.");
            }

            decimal tienXacMinh = 0m;
            decimal? tienThua = null;

            // Xử lý phương thức thanh toán tiền mặt
            if (request.PhuongThuc == "CASH")
            {
                decimal tienKhachDua = request.TienKhachDua ?? request.SoTien;
                if (tienKhachDua < soTienCanThu)
                {
                    throw new ArgumentException($"Số tiền khách đưa ({tienKhachDua:N0}đ) chưa đủ để thanh toán ({soTienCanThu:N0}đ).");
                }

                tienXacMinh = soTienCanThu;
                tienThua = tienKhachDua - soTienCanThu;
            }
            // Xử lý phương thức thanh toán chuyển khoản
            else if (request.PhuongThuc == "BANK_TRANSFER")
            {
                if (request.SoTien <= 0m)
                {
                    throw new ArgumentException("Số tiền chuyển khoản phải lớn hơn 0.");
                }
                tienXacMinh = Math.Min(request.SoTien, soTienCanThu);
            }
            else
            {
                throw new ArgumentException($"Phương thức thanh toán {request.PhuongThuc} không được hỗ trợ.");
            }

            var paymentRecord = new PosPaymentRecord
            {
                MaDonHang = order.MaDonHang,
                PhuongThuc = request.PhuongThuc,
                SoTienYeuCau = soTienCanThu,
                SoTienDaXacMinh = tienXacMinh,
                TienKhachDua = request.TienKhachDua,
                TienThua = tienThua,
                MaThamChieuChuyenKhoan = request.MaThamChieuChuyenKhoan,
                TrangThai = "PAID",
                MaCaLamViec = session.MaCaLamViec,
                MaThaoTac = request.MaThaoTac,
                ThoiDiem = DateTime.UtcNow
            };

            order.Payments.Add(paymentRecord);
            order.SoTienDaThanhToan += tienXacMinh;

            if (order.SoDuConThu == 0m)
            {
                order.TrangThaiThanhToan = "PAID";
            }
            else
            {
                order.TrangThaiThanhToan = "PARTIAL";
            }

            order.Version++;
            await _orderRepo.UpdateOrderAsync(order);

            if (_paymentService != null)
            {
                await _paymentService.RecordPaymentAsync(paymentRecord);
            }

            return paymentRecord;
        }
    }
}
