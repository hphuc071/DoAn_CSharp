namespace FourChang.Presentation.Shared.Contracts.Common;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record TableData(string[] Columns, string[][] Rows);
