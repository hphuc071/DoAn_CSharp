using System;

namespace FourChang.Desktop.Features.Pos.Models
{
    public enum WorkSessionStatus
    {
        OPEN,
        LOCKED,
        CLOSED,
        HANDED_OVER
    }

    public class WorkSession
    {
        public string MaCaLamViec { get; set; } = Guid.NewGuid().ToString("N");
        public string MaNhanVien { get; set; } = null!;
        public string TenNhanVienSnapshot { get; set; } = null!;
        public string MaMay { get; set; } = null!;
        public string TenCa { get; set; } = null!;
        public DateTime StartAt { get; set; } = DateTime.UtcNow;
        public DateTime? EndAt { get; set; }
        public decimal TienDauCa { get; set; }
        public decimal? TienThucDem { get; set; }
        public decimal TienDuKien { get; set; }
        public decimal ChenhLech { get; set; }
        public string? LyDoChenhLech { get; set; }
        public WorkSessionStatus Status { get; set; } = WorkSessionStatus.OPEN;
    }

    public class PosTerminal
    {
        public string MaMay { get; set; } = null!;
        public bool IsAuthorized { get; set; }
        public bool IsActive { get; set; }
    }

    public class NhanVienProfile
    {
        public string MaNhanVien { get; set; } = null!;
        public string HoTen { get; set; } = null!;
        public bool IsActive { get; set; }
    }

    public class OpenShiftRequest
    {
        public string MaMay { get; set; } = null!;
        public string MaNhanVien { get; set; } = null!;
        public string TenCa { get; set; } = null!;
        public decimal TienDauCa { get; set; }
    }

    public class CloseShiftRequest
    {
        public string MaCaLamViec { get; set; } = null!;
        public decimal TienThucDem { get; set; }
        public string? LyDoChenhLech { get; set; }
    }

    public class ShiftSummaryDto
    {
        public decimal TienDauCa { get; set; }
        public decimal TongThuCash { get; set; }
        public decimal TongHoanCash { get; set; }
        public decimal TienDuKien { get; set; }
        public decimal TongChuyenKhoan { get; set; }
    }
}
