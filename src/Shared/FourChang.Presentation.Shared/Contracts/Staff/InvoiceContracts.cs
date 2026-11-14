using FourChang.Presentation.Shared.Contracts.Admin;
namespace FourChang.Presentation.Shared.Contracts.Staff;
/// <summary>Chỉ đọc hóa đơn gốc cho POS, không trả nhật ký quản trị hoặc quyền ghi.</summary>
public sealed record StaffInvoiceSnapshot(AdminInvoiceView HoaDon, string SnapshotJson);
