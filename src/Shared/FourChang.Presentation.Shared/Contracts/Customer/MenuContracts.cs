namespace FourChang.Presentation.Shared.Contracts.Customer;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record MenuProduct(long Id, long MaMon, string Name, string Category, decimal Price, int ImageIndex, bool Available, string Badge);
