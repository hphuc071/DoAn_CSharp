using FourChang.Presentation.Shared.Integration;
namespace FourChang.Desktop.Data;

public static class LiveSession
{
    public static bool Preview { get; set; }
    public static SessionResult? Current { get; set; }
    public static CafeApiClient Api { get; } = Create();
    private static CafeApiClient Create()
    {
        // Client chỉ đọc URL và khóa thiết bị; không nạp mật khẩu SQL vào WinForms.
        var settings=new Dictionary<string,string>(StringComparer.Ordinal);
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"FourChang.sln")))dir=dir.Parent;
        var file=dir is null?null:Path.Combine(dir.FullName,".env");
        if(file is not null&&File.Exists(file))foreach(var line in File.ReadLines(file))
        {
            var split=line.IndexOf('=');if(split<=0)continue;var key=line[..split].Trim();
            if(key is "FOURCHANG_API_URL" or "FOURCHANG_POS_KEY")settings[key]=line[(split+1)..].Trim().Trim('"','\'');
        }
        var url=Environment.GetEnvironmentVariable("FOURCHANG_API_URL")??settings.GetValueOrDefault("FOURCHANG_API_URL")??"http://127.0.0.1:5181";
        return new CafeApiClient(new HttpClient{BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20)}){PosKey=Environment.GetEnvironmentVariable("FOURCHANG_POS_KEY")??settings.GetValueOrDefault("FOURCHANG_POS_KEY")};
    }
}
