# DoAn_CSharp
#Trương Quốc Thái : 3124411271
#Nguyễn Đức Tài : 3124411262
#Nguyễn Hoàng Phúc : 3124411237
#Trần Văn Thiện: 3124411288

## Kiểm thử C# xUnit

Toàn bộ test thực thi viết bằng C#, dùng một project `tests/FourChang.Tests/FourChang.Tests.csproj`, bên trong chia theo actor và UC. Từ thư mục gốc chạy `dotnet test FourChang.sln`. Cần Windows, .NET 8, SQL Docker đang chạy và `.env` đã cấu hình; mỗi kịch bản dùng database riêng. Xem [hướng dẫn kiểm thử](tests/README.md) để lọc actor/UC và xuất TRX.
