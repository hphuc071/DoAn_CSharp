using System;
using System.Threading.Tasks;
using FourChang.Desktop.Features.Pos.Models;

namespace FourChang.Desktop.Features.Pos.Services
{
    public interface IWorkSessionRepository
    {
        Task<WorkSession?> GetActiveShiftByTerminalAsync(string maMay);
        Task<WorkSession?> GetActiveShiftByStaffAsync(string maNhanVien);
        Task<WorkSession?> GetByIdAsync(string maCaLamViec);
        Task CreateAsync(WorkSession session);
        Task UpdateAsync(WorkSession session);
    }

    public interface ITerminalRepository
    {
        Task<PosTerminal?> GetByIdAsync(string maMay);
    }

    public interface INhanVienRepository
    {
        Task<NhanVienProfile?> GetByIdAsync(string maNhanVien);
    }

    public interface IPaymentRepository
    {
        Task<decimal> GetTotalVerifiedCashCollectedAsync(string maCaLamViec);
        Task<decimal> GetTotalCashRefundedAsync(string maCaLamViec);
    }

    public class PosShiftService
    {
        private readonly IWorkSessionRepository _sessionRepo;
        private readonly ITerminalRepository _terminalRepo;
        private readonly INhanVienRepository _nhanVienRepo;
        private readonly IPaymentRepository _paymentRepo;

        public PosShiftService(
            IWorkSessionRepository sessionRepo,
            ITerminalRepository terminalRepo,
            INhanVienRepository nhanVienRepo,
            IPaymentRepository paymentRepo)
        {
            _sessionRepo = sessionRepo;
            _terminalRepo = terminalRepo;
            _nhanVienRepo = nhanVienRepo;
            _paymentRepo = paymentRepo;
        }

        public async Task<WorkSession> OpenShiftAsync(OpenShiftRequest request)
        {
            var terminal = await _terminalRepo.GetByIdAsync(request.MaMay);
            if (terminal == null || !terminal.IsAuthorized || !terminal.IsActive)
            {
                throw new InvalidOperationException("Thiết bị POS không được cấp quyền truy cập.");
            }

            var staff = await _nhanVienRepo.GetByIdAsync(request.MaNhanVien);
            if (staff == null || !staff.IsActive)
            {
                throw new InvalidOperationException("Hồ sơ nhân viên không tồn tại hoặc đã ngừng làm việc.");
            }

            if (request.TienDauCa < 0)
            {
                throw new ArgumentException("Tiền đầu ca không được nhỏ hơn 0.");
            }

            var existingTerminalShift = await _sessionRepo.GetActiveShiftByTerminalAsync(request.MaMay);
            if (existingTerminalShift != null)
            {
                throw new InvalidOperationException("Máy POS hiện tại đã có ca làm việc chưa kết thúc.");
            }

            var existingStaffShift = await _sessionRepo.GetActiveShiftByStaffAsync(request.MaNhanVien);
            if (existingStaffShift != null)
            {
                throw new InvalidOperationException("Nhân viên đã chọn đang có một ca làm việc khác chưa kết thúc.");
            }

            var session = new WorkSession
            {
                MaMay = request.MaMay,
                MaNhanVien = staff.MaNhanVien,
                TenNhanVienSnapshot = staff.HoTen,
                TenCa = request.TenCa,
                TienDauCa = request.TienDauCa,
                StartAt = DateTime.UtcNow,
                Status = WorkSessionStatus.OPEN
            };

            await _sessionRepo.CreateAsync(session);
            return session;
        }

        public async Task<WorkSession> CloseShiftAsync(CloseShiftRequest request)
        {
            var session = await _sessionRepo.GetByIdAsync(request.MaCaLamViec);
            if (session == null || session.Status != WorkSessionStatus.OPEN)
            {
                throw new InvalidOperationException("Ca làm việc không tồn tại hoặc đã kết thúc.");
            }

            decimal thuCash = await _paymentRepo.GetTotalVerifiedCashCollectedAsync(session.MaCaLamViec);
            decimal hoanCash = await _paymentRepo.GetTotalCashRefundedAsync(session.MaCaLamViec);

            decimal tienDuKien = session.TienDauCa + thuCash - hoanCash;
            decimal chenhLech = request.TienThucDem - tienDuKien;

            if (chenhLech != 0 && string.IsNullOrWhiteSpace(request.LyDoChenhLech))
            {
                throw new ArgumentException("Phát sinh chênh lệch tiền mặt. Vui lòng nhập lý do giải trình.");
            }

            session.TienDuKien = tienDuKien;
            session.TienThucDem = request.TienThucDem;
            session.ChenhLech = chenhLech;
            session.LyDoChenhLech = request.LyDoChenhLech;
            session.EndAt = DateTime.UtcNow;
            session.Status = WorkSessionStatus.CLOSED;

            await _sessionRepo.UpdateAsync(session);
            return session;
        }
    }
}