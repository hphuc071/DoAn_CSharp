namespace FourChang.Presentation.Shared.Contracts.Customer;

public sealed record ChangeTemporaryPassword(string MatKhauMoi, Guid MaThaoTac, byte[] PhienBanDuLieu, string XacNhanMatKhau);
