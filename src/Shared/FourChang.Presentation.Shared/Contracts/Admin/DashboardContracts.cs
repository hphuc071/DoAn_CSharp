namespace FourChang.Presentation.Shared.Contracts.Admin;

// Hợp đồng dữ liệu; giữ nguyên tên trường để các phân hệ tiếp tục dùng cùng API.
public sealed record DashboardData(decimal Revenue, int CompletedOrders, int PendingRefunds, int LowIngredients);
