namespace FourChang.Tests;

/// <summary>Điểm vào nội bộ cho worker WinForms; người dùng vẫn chạy test bằng dotnet test.</summary>
internal static class Program
{

    /// <summary>
    /// Chọn đúng kịch bản WinForms đã đăng ký và chạy trên luồng STA; trả mã lỗi cho xUnit nếu thao tác không đạt.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        // Chỉ nhận ba kịch bản đã có, không thực thi đường dẫn hoặc lệnh do bên ngoài gửi vào.
        if (args.Length != 3 || args[0] != "--desktop-scenario")
        {
            Console.Error.WriteLine("Chạy kiểm thử bằng: dotnet test FourChang.sln");
            return 2;
        }

        try
        {
            return args[1] switch
            {
                "UC-ADM-01" => CustomersUiTests.RunHarness([args[2]]),
                "UC-ADM-02" => AccessUiTests.RunHarness([args[2]]),
                "UC-POS-01" => EntryUiTests.RunHarness([args[2]]),
                _ => 2
            };
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
