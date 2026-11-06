using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Common;
namespace FourChang.Desktop.Shared.Session;

public static class LiveSession
{
    public static bool Preview { get; set; }
    public static SessionResult? Current { get; set; }
    public static CafeApiClient Api { get; } = Create();
    private static CafeApiClient Create()
    {
        // Client chỉ đọc URL từ .env; khóa từng máy được đọc riêng từ kho DPAPI.
        var settings=new Dictionary<string,string>(StringComparer.Ordinal);
        var dir=new DirectoryInfo(AppContext.BaseDirectory);
        while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"FourChang.sln")))dir=dir.Parent;
        var file=dir is null?null:Path.Combine(dir.FullName,".env");
        if(file is not null&&File.Exists(file))foreach(var line in File.ReadLines(file))
        {
            var split=line.IndexOf('=');if(split<=0)continue;var key=line[..split].Trim();
            if(key is "FOURCHANG_API_URL")settings[key]=line[(split+1)..].Trim().Trim('"','\'');
        }
        var url=Environment.GetEnvironmentVariable("FOURCHANG_API_URL")??settings.GetValueOrDefault("FOURCHANG_API_URL")??"http://127.0.0.1:15181";
        var credential=FourChang.Desktop.Shared.Security.PosDeviceCredentials.Load();
        return new CafeApiClient(new HttpClient{BaseAddress=new Uri(url),Timeout=TimeSpan.FromSeconds(20)}){PosKey=credential?.KhoaThietBi,PosMachine=credential?.MaMay??Environment.MachineName.ToUpperInvariant()};
    }
}
