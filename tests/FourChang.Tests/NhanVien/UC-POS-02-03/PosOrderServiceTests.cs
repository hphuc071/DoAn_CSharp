using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourChang.Desktop.Features.Pos.Models;
using FourChang.Desktop.Features.Pos.Services;
using Xunit;

namespace FourChang.Tests.NhanVien
{
    public class PosOrderServiceTests
    {
        // Giả lập MenuRepository trong bộ nhớ
        private class FakeMenuRepo : IMenuRepository
        {
            public Dictionary<string, MonDetail> MonStore = new();
            public Dictionary<string, ToppingItem> ToppingStore = new();
            public Dictionary<string, RecipeDetail> RecipeStore = new();

            public Task<MonDetail?> GetMonByIdAsync(string maMon) =>
                Task.FromResult(MonStore.GetValueOrDefault(maMon));

            public Task<ToppingItem?> GetToppingByIdAsync(string maTopping) =>
                Task.FromResult(ToppingStore.GetValueOrDefault(maTopping));

            public Task<RecipeDetail?> GetRecipeByVariantAsync(string maMon, string size, bool isHot)
            {
                var key = $"{maMon}_{size}_{(isHot ? "HOT" : "COLD")}";
                return Task.FromResult(RecipeStore.GetValueOrDefault(key));
            }

            public Task<RecipeDetail?> GetRecipeByToppingAsync(string maTopping) =>
                Task.FromResult(RecipeStore.GetValueOrDefault(maTopping));
        }

        // Giả lập OrderRepository trong bộ nhớ
        private class FakeOrderRepo : IOrderRepository
        {
            public Dictionary<string, PosOrder> OrderStore = new();

            public Task<PosOrder?> GetByIdAsync(string maDonHang) =>
                Task.FromResult(OrderStore.GetValueOrDefault(maDonHang));

            public Task CreateOrderAsync(PosOrder order)
            {
                OrderStore[order.MaDonHang] = order;
                return Task.CompletedTask;
            }

            public Task UpdateOrderAsync(PosOrder order)
            {
                OrderStore[order.MaDonHang] = order;
                return Task.CompletedTask;
            }

            public Task<List<PosOrder>> GetQueueAsync() =>
                Task.FromResult(OrderStore.Values.ToList());
        }

        // Giả lập InventoryService trong bộ nhớ
        private class FakeInventoryService : IInventoryService
        {
            public Dictionary<string, StockItem> Stocks = new();
            public List<OrderLineIngredientSnapshot> Released = new();
            public Dictionary<string, decimal> Consumed = new();

            public Task<StockItem?> GetStockAsync(string maNguyenLieu) =>
                Task.FromResult(Stocks.GetValueOrDefault(maNguyenLieu));

            public Task<bool> CheckAvailabilityAsync(Dictionary<string, decimal> required)
            {
                foreach (var (ingId, needed) in required)
                {
                    if (!Stocks.TryGetValue(ingId, out var stock) || stock.TonKhaDung < needed)
                        return Task.FromResult(false);
                }
                return Task.FromResult(true);
            }

            public Task<bool> ReserveInventoryForOrderAsync(PosOrder order)
            {
                foreach (var line in order.LineItems)
                {
                    foreach (var ing in line.IngredientsSnapshot)
                    {
                        if (Stocks.TryGetValue(ing.MaNguyenLieu, out var stock))
                        {
                            stock.TongGiuCho += ing.TongLuongDinhMuc;
                        }
                    }
                }
                return Task.FromResult(true);
            }

            public Task ReleaseReservationAsync(string maDonHang, List<OrderLineIngredientSnapshot> ingredientsToRelease)
            {
                Released.AddRange(ingredientsToRelease);
                foreach (var ing in ingredientsToRelease)
                {
                    if (Stocks.TryGetValue(ing.MaNguyenLieu, out var stock))
                    {
                        stock.TongGiuCho -= ing.TongLuongDinhMuc;
                    }
                }
                return Task.CompletedTask;
            }

            public Task ConsumeReservedInventoryAsync(string maDonHang, string maDong, Dictionary<string, decimal> ingredientsToConsume)
            {
                foreach (var (ingId, amount) in ingredientsToConsume)
                {
                    Consumed[ingId] = Consumed.GetValueOrDefault(ingId) + amount;
                    if (Stocks.TryGetValue(ingId, out var stock))
                    {
                        stock.TonThucTe -= amount;
                        stock.TongGiuCho -= amount;
                    }
                }
                return Task.CompletedTask;
            }
        }

        // Giả lập WorkSessionRepository trong bộ nhớ
        private class FakeSessionRepo : IWorkSessionRepository
        {
            public Dictionary<string, WorkSession> Sessions = new();

            public Task<WorkSession?> GetActiveShiftByTerminalAsync(string maMay) =>
                Task.FromResult(Sessions.Values.FirstOrDefault(s => s.MaMay == maMay && s.Status == WorkSessionStatus.OPEN));

            public Task<WorkSession?> GetActiveShiftByStaffAsync(string maNhanVien) =>
                Task.FromResult(Sessions.Values.FirstOrDefault(s => s.MaNhanVien == maNhanVien && s.Status == WorkSessionStatus.OPEN));

            public Task<WorkSession?> GetByIdAsync(string maCaLamViec) =>
                Task.FromResult(Sessions.GetValueOrDefault(maCaLamViec));

            public Task CreateAsync(WorkSession session)
            {
                Sessions[session.MaCaLamViec] = session;
                return Task.CompletedTask;
            }

            public Task UpdateAsync(WorkSession session)
            {
                Sessions[session.MaCaLamViec] = session;
                return Task.CompletedTask;
            }
        }

        // Giả lập PaymentService trong bộ nhớ
        private class FakePaymentService : IPaymentService
        {
            public Dictionary<string, PosPaymentRecord> PaymentsByOp = new();

            public Task<PosPaymentRecord?> GetPaymentByOperationAsync(string maThaoTac) =>
                Task.FromResult(PaymentsByOp.GetValueOrDefault(maThaoTac));

            public Task<PosPaymentRecord> RecordPaymentAsync(PosPaymentRecord payment)
            {
                PaymentsByOp[payment.MaThaoTac] = payment;
                return Task.FromResult(payment);
            }
        }

        private (PosOrderService service, FakeMenuRepo menu, FakeSessionRepo sessions, FakeOrderRepo orders, FakeInventoryService inventory, FakePaymentService payments) Setup()
        {
            var menu = new FakeMenuRepo();
            var sessions = new FakeSessionRepo();
            var orders = new FakeOrderRepo();
            var inventory = new FakeInventoryService();
            var payments = new FakePaymentService();

            // Khởi tạo món cà phê mẫu
            menu.MonStore["CF01"] = new MonDetail
            {
                MaMon = "CF01",
                TenMon = "Cà phê sữa đá",
                IsPublished = true,
                Sizes = new()
                {
                    new MenuItemOption { Size = "S", BasePrice = 25000, IsHotAllowed = true, IsColdAllowed = true },
                    new MenuItemOption { Size = "M", BasePrice = 30000, IsHotAllowed = true, IsColdAllowed = true },
                    new MenuItemOption { Size = "L", BasePrice = 35000, IsHotAllowed = false, IsColdAllowed = true }
                }
            };

            // Khởi tạo công thức cho CF01 size M Cold
            menu.RecipeStore["CF01_M_COLD"] = new RecipeDetail
            {
                MaCongThuc = "REC_CF01_M_COLD",
                PhienBan = 1,
                MaMon = "CF01",
                Size = "M",
                IsHot = false,
                Ingredients = new()
                {
                    new RecipeIngredient { MaNguyenLieu = "NL_CF", TenNguyenLieu = "Cà phê bột", DonViCoSo = "g", DinhMucCoSo = 20, LoaiDinhMuc = "FIXED", TyLeHaoHut = 0m },
                    new RecipeIngredient { MaNguyenLieu = "NL_SUA", TenNguyenLieu = "Sữa đặc", DonViCoSo = "ml", DinhMucCoSo = 30, LoaiDinhMuc = "SUGAR", TyLeHaoHut = 0m },
                    new RecipeIngredient { MaNguyenLieu = "NL_DA", TenNguyenLieu = "Đá bi", DonViCoSo = "g", DinhMucCoSo = 150, LoaiDinhMuc = "ICE", TyLeHaoHut = 0m }
                }
            };

            // Khởi tạo công thức cho CF01 size S Hot
            menu.RecipeStore["CF01_S_HOT"] = new RecipeDetail
            {
                MaCongThuc = "REC_CF01_S_HOT",
                PhienBan = 1,
                MaMon = "CF01",
                Size = "S",
                IsHot = true,
                Ingredients = new()
                {
                    new RecipeIngredient { MaNguyenLieu = "NL_CF", TenNguyenLieu = "Cà phê bột", DonViCoSo = "g", DinhMucCoSo = 15, LoaiDinhMuc = "FIXED", TyLeHaoHut = 0m }
                }
            };

            // Khởi tạo topping mẫu
            menu.ToppingStore["TOP_FLAN"] = new ToppingItem
            {
                MaTopping = "TOP_FLAN",
                TenTopping = "Bánh Flan",
                GiaMoiPhan = 10000,
                GioiHanSoPhan = 2
            };

            menu.RecipeStore["TOP_FLAN"] = new RecipeDetail
            {
                MaCongThuc = "REC_TOP_FLAN",
                PhienBan = 1,
                MaTopping = "TOP_FLAN",
                Ingredients = new()
                {
                    new RecipeIngredient { MaNguyenLieu = "NL_TRUNG", TenNguyenLieu = "Trứng", DonViCoSo = "qua", DinhMucCoSo = 1, LoaiDinhMuc = "FIXED", TyLeHaoHut = 0m }
                }
            };

            // Khởi tạo kho nguyên liệu
            inventory.Stocks["NL_CF"] = new StockItem { MaNguyenLieu = "NL_CF", TenNguyenLieu = "Cà phê bột", DonViCoSo = "g", TonThucTe = 1000, TongGiuCho = 0, TonAnToan = 50 };
            inventory.Stocks["NL_SUA"] = new StockItem { MaNguyenLieu = "NL_SUA", TenNguyenLieu = "Sữa đặc", DonViCoSo = "ml", TonThucTe = 2000, TongGiuCho = 0, TonAnToan = 100 };
            inventory.Stocks["NL_DA"] = new StockItem { MaNguyenLieu = "NL_DA", TenNguyenLieu = "Đá bi", DonViCoSo = "g", TonThucTe = 5000, TongGiuCho = 0, TonAnToan = 200 };
            inventory.Stocks["NL_TRUNG"] = new StockItem { MaNguyenLieu = "NL_TRUNG", TenNguyenLieu = "Trứng", DonViCoSo = "qua", TonThucTe = 100, TongGiuCho = 0, TonAnToan = 10 };

            // Khởi tạo ca làm việc OPEN
            sessions.Sessions["SHIFT_01"] = new WorkSession
            {
                MaCaLamViec = "SHIFT_01",
                MaNhanVien = "NV01",
                TenNhanVienSnapshot = "Nguyen Van A",
                MaMay = "POS_01",
                TenCa = "Sáng",
                TienDauCa = 1000000,
                Status = WorkSessionStatus.OPEN
            };

            var service = new PosOrderService(menu, sessions, orders, inventory, payments);
            return (service, menu, sessions, orders, inventory, payments);
        }

        // Test 1: Tùy chỉnh và công thức (POS-02, POS-03)
        [Fact]
        public async Task BuildLineItem_ApDungDuongDaVaToppingVaoDinhMuc_ChinhXac()
        {
            var (service, _, _, _, _, _) = Setup();

            var req = new ConfigureItemRequest
            {
                MaMon = "CF01",
                Size = "M",
                IsHot = false,
                MucDuongPercent = 50,
                MucDaPercent = 70,
                SoLy = 2,
                Toppings = new() { new ToppingSelectionDto { MaTopping = "TOP_FLAN", SoPhan = 1 } }
            };

            var line = await service.BuildAndValidateLineItemAsync(req);

            // Kiểm tra giá: size M (30.000) + 1 phần Flan (10.000) = 40.000/ly, 2 ly = 80.000
            Assert.Equal(40000m, line.DonGiaLy);
            Assert.Equal(80000m, line.ThanhTienDong);

            // Kiểm tra định mức sữa đặc (SUGAR 50% của 30ml = 15ml/ly, 2 ly = 30ml)
            var suaSnapshot = line.IngredientsSnapshot.First(x => x.MaNguyenLieu == "NL_SUA");
            Assert.Equal(15m, suaSnapshot.DinhMucMoiLy);
            Assert.Equal(30m, suaSnapshot.TongLuongDinhMuc);

            // Kiểm tra định mức đá (ICE 70% của 150g = 105g/ly, 2 ly = 210g)
            var daSnapshot = line.IngredientsSnapshot.First(x => x.MaNguyenLieu == "NL_DA");
            Assert.Equal(105m, daSnapshot.DinhMucMoiLy);
            Assert.Equal(210m, daSnapshot.TongLuongDinhMuc);

            // Kiểm tra định mức cà phê (FIXED không đổi = 20g/ly, 2 ly = 40g)
            var cfSnapshot = line.IngredientsSnapshot.First(x => x.MaNguyenLieu == "NL_CF");
            Assert.Equal(20m, cfSnapshot.DinhMucMoiLy);
            Assert.Equal(40m, cfSnapshot.TongLuongDinhMuc);

            // Kiểm tra nguyên liệu topping được gộp vào snapshot (1 trứng/ly, 2 ly = 2 quả)
            var trungSnapshot = line.IngredientsSnapshot.First(x => x.MaNguyenLieu == "NL_TRUNG");
            Assert.Equal(1m, trungSnapshot.DinhMucMoiLy);
            Assert.Equal(2m, trungSnapshot.TongLuongDinhMuc);
        }

        // Test 2: Ràng buộc đồ nóng không đá (BR-11)
        [Fact]
        public async Task BuildLineItem_DoUongNongChonDa_BaoLoi()
        {
            var (service, _, _, _, _, _) = Setup();

            var req = new ConfigureItemRequest
            {
                MaMon = "CF01",
                Size = "S",
                IsHot = true,
                MucDaPercent = 50, // Lỗi: nóng không được chọn đá
                SoLy = 1
            };

            await Assert.ThrowsAsync<ArgumentException>(() => service.BuildAndValidateLineItemAsync(req));
        }

        // Test 3: Tồn kho và món hết hàng (POS-02, POS-03 - Giữ chỗ khi tạo đơn, trừ khi bắt đầu pha)
        [Fact]
        public async Task CreateOrder_GiuChoNguyenLieu_VaTruKhoKhiBatDauPha()
        {
            var (service, _, _, orders, inventory, _) = Setup();

            var createReq = new CreatePosOrderRequest
            {
                MaCaLamViec = "SHIFT_01",
                FulfillmentType = "AT_TABLE",
                SoBan = "B01",
                Items = new()
                {
                    new ConfigureItemRequest
                    {
                        MaMon = "CF01",
                        Size = "M",
                        IsHot = false,
                        MucDuongPercent = 100,
                        MucDaPercent = 100,
                        SoLy = 2
                    }
                }
            };

            // 1. Tạo đơn: Giữ chỗ 2 ly cà phê (40g CF)
            var order = await service.CreatePosOrderAsync(createReq);
            Assert.Equal("PENDING_CONFIRMATION", order.OrderStatus);
            Assert.Equal(40m, inventory.Stocks["NL_CF"].TongGiuCho);
            Assert.Equal(1000m, inventory.Stocks["NL_CF"].TonThucTe); // Tồn thực tế chưa giảm

            // 2. Tiếp nhận đơn: Trạng thái chuyển ACCEPTED, kho thực tế vẫn chưa giảm (POS-03 bước 5)
            await service.AcceptOrderAsync(new AcceptOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" });
            Assert.Equal("ACCEPTED", order.OrderStatus);
            Assert.Equal(1000m, inventory.Stocks["NL_CF"].TonThucTe);

            // 3. Bắt đầu pha 1 ly (POS-03 bước 8 & A3): Giảm tồn thực tế 20g, giảm giữ chỗ 20g (BR-02)
            var lineId = order.LineItems[0].MaDong;
            await service.StartPreparingLineItemAsync(new StartLinePrepRequest
            {
                MaDonHang = order.MaDonHang,
                MaDong = lineId,
                SoLyBatDau = 1,
                MaCaLamViec = "SHIFT_01"
            });

            Assert.Equal("PREPARING", order.OrderStatus);
            Assert.Equal(980m, inventory.Stocks["NL_CF"].TonThucTe); // Đã trừ 20g thực tế
            Assert.Equal(20m, inventory.Stocks["NL_CF"].TongGiuCho); // Đã giảm 20g giữ chỗ
        }

        // Test 4: Hủy phần chưa pha giải phóng giữ chỗ (POS-04)
        [Fact]
        public async Task CancelLineItem_GiaiPhongGiuChoPhanChuaPha()
        {
            var (service, _, _, _, inventory, _) = Setup();

            var createReq = new CreatePosOrderRequest
            {
                MaCaLamViec = "SHIFT_01",
                FulfillmentType = "AT_TABLE",
                SoBan = "B02",
                Items = new()
                {
                    new ConfigureItemRequest
                    {
                        MaMon = "CF01",
                        Size = "M",
                        IsHot = false,
                        MucDuongPercent = 100,
                        MucDaPercent = 100,
                        SoLy = 3
                    }
                }
            };

            var order = await service.CreatePosOrderAsync(createReq);
            await service.AcceptOrderAsync(new AcceptOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" });

            // Bắt đầu pha 1 ly trong số 3 ly
            var lineId = order.LineItems[0].MaDong;
            await service.StartPreparingLineItemAsync(new StartLinePrepRequest
            {
                MaDonHang = order.MaDonHang,
                MaDong = lineId,
                SoLyBatDau = 1,
                MaCaLamViec = "SHIFT_01"
            });

            // Hủy 2 ly còn lại chưa pha
            await service.CancelLineItemBeforePrepAsync(new CancelLineItemRequest
            {
                MaDonHang = order.MaDonHang,
                MaDong = lineId,
                SoLyHuy = 2,
                LyDo = "Khách đổi ý hủy 2 ly chưa pha",
                MaCaLamViec = "SHIFT_01"
            });

            // Giữ chỗ của 2 ly bị hủy (40g) đã được giải phóng
            Assert.Equal(0m, inventory.Stocks["NL_CF"].TongGiuCho);
            Assert.Equal(2, order.LineItems[0].SoLyDaHuy);
        }

        // Test 5: Tiếp nhận đơn Web đã xác minh đủ tiền (BR-16)
        [Fact]
        public async Task AcceptOrder_DonWebChuaThanhToan_BiTuChoi()
        {
            var (service, _, _, orders, _, _) = Setup();

            var webOrder = new PosOrder
            {
                MaDonHang = "WEB_001",
                Source = "WEB",
                FulfillmentType = "TAKEAWAY",
                TenNguoiNhan = "Nguyen Khach",
                TongTienHang = 60000,
                TongThanhToan = 60000,
                OrderStatus = "PENDING_CONFIRMATION",
                TrangThaiThanhToan = "UNPAID" // Chưa trả tiền
            };
            orders.OrderStore[webOrder.MaDonHang] = webOrder;

            // Bị từ chối tiếp nhận vì Web bắt buộc phải thanh toán trước
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AcceptOrderAsync(new AcceptOrderRequest { MaDonHang = "WEB_001", MaCaLamViec = "SHIFT_01" }));

            // Khi đã thanh toán đủ -> tiếp nhận thành công
            webOrder.TrangThaiThanhToan = "PAID";
            webOrder.SoTienDaThanhToan = 60000;
            var accepted = await service.AcceptOrderAsync(new AcceptOrderRequest { MaDonHang = "WEB_001", MaCaLamViec = "SHIFT_01" });
            Assert.Equal("ACCEPTED", accepted.OrderStatus);
        }

        // Test 6: Bàn giao bị chặn khi chưa thu đủ tiền (POS-03 E9, BR-16)
        [Fact]
        public async Task ServeOrder_ChuaThuDuTien_BiChanBanGiao()
        {
            var (service, _, _, _, _, _) = Setup();

            var createReq = new CreatePosOrderRequest
            {
                MaCaLamViec = "SHIFT_01",
                FulfillmentType = "AT_TABLE",
                SoBan = "B05",
                Items = new()
                {
                    new ConfigureItemRequest { MaMon = "CF01", Size = "M", IsHot = false, SoLy = 1 }
                }
            };

            var order = await service.CreatePosOrderAsync(createReq);
            await service.AcceptOrderAsync(new AcceptOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" });

            var lineId = order.LineItems[0].MaDong;
            await service.StartPreparingLineItemAsync(new StartLinePrepRequest { MaDonHang = order.MaDonHang, MaDong = lineId, SoLyBatDau = 1, MaCaLamViec = "SHIFT_01" });
            await service.MarkLineReadyAsync(new MarkLineReadyRequest { MaDonHang = order.MaDonHang, MaDong = lineId, SoLyPhaXong = 1, MaCaLamViec = "SHIFT_01" });

            // Cố gắng bàn giao khi chưa thanh toán -> bị chặn
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ServeOrderAsync(new ServeOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" }));

            // Thu tiền mặt đủ
            await service.ProcessPaymentAsync(new ProcessPaymentRequest
            {
                MaDonHang = order.MaDonHang,
                PhuongThuc = "CASH",
                SoTien = order.TongThanhToan,
                TienKhachDua = 50000,
                MaCaLamViec = "SHIFT_01"
            });

            // Sau khi thu đủ -> bàn giao thành công
            var served = await service.ServeOrderAsync(new ServeOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" });
            Assert.Equal("SERVED", served.OrderStatus);

            // Hoàn tất đơn
            var completed = await service.CompleteOrderAsync(new CompleteOrderRequest { MaDonHang = order.MaDonHang, MaCaLamViec = "SHIFT_01" });
            Assert.Equal("COMPLETED", completed.OrderStatus);
        }

        // Test 7: Thanh toán tiền mặt tính tiền thừa và chống trùng mã thao tác (BR-14, BR-19)
        [Fact]
        public async Task ProcessPayment_TinhTienThuaVaChongTrungMaThaoTac()
        {
            var (service, _, _, _, _, _) = Setup();

            var createReq = new CreatePosOrderRequest
            {
                MaCaLamViec = "SHIFT_01",
                FulfillmentType = "TAKEAWAY",
                TenNguoiNhan = "Le Thi B",
                Items = new()
                {
                    new ConfigureItemRequest { MaMon = "CF01", Size = "M", IsHot = false, SoLy = 1 }
                }
            };

            var order = await service.CreatePosOrderAsync(createReq);
            var opKey = Guid.NewGuid().ToString("N");

            // Khách đưa 50.000đ cho đơn 30.000đ -> tiền thừa 20.000đ
            var payment = await service.ProcessPaymentAsync(new ProcessPaymentRequest
            {
                MaDonHang = order.MaDonHang,
                PhuongThuc = "CASH",
                SoTien = 30000,
                TienKhachDua = 50000,
                MaCaLamViec = "SHIFT_01",
                MaThaoTac = opKey
            });

            Assert.Equal(20000m, payment.TienThua);
            Assert.Equal(30000m, payment.SoTienDaXacMinh);
            Assert.Equal("PAID", order.TrangThaiThanhToan);

            // Gửi lại cùng mã thao tác -> trả lại kết quả cũ, không thu lần hai
            var replay = await service.ProcessPaymentAsync(new ProcessPaymentRequest
            {
                MaDonHang = order.MaDonHang,
                PhuongThuc = "CASH",
                SoTien = 30000,
                TienKhachDua = 50000,
                MaCaLamViec = "SHIFT_01",
                MaThaoTac = opKey
            });

            Assert.Equal(payment.MaThanhToan, replay.MaThanhToan);
        }
    }
}
