using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace FourChang.Desktop.Shared.Security;

// Khóa thiết bị nằm ngoài repository, được Windows DPAPI bảo vệ theo người dùng hiện tại.
// Không lưu khóa thô vào .env, audit, file JSON thường hoặc source code.
public static class PosDeviceCredentials
{
    public sealed record Credential(string MaMay, string KhoaThietBi);
    private sealed record Stored(string MaMay, string DuLieuDaBaoVe);
    private static string FilePath => Environment.GetEnvironmentVariable("FOURCHANG_POS_CREDENTIALS_PATH") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FourChang","pos-device.json");
    public static void Save(string machine, string key)
    {
        if(!string.Equals(machine,Environment.MachineName,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Chỉ cấu hình trực tiếp được cho máy đang sử dụng.");
        var raw=Encoding.UTF8.GetBytes(key);
        try
        {
            var value=new Stored(machine.ToUpperInvariant(),Convert.ToBase64String(Protect(raw,false)));
            var path=FilePath;Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(value));File.Move(temporary,path,true);
        }
        finally{CryptographicOperations.ZeroMemory(raw);}
    }
    public static Credential? Load()
    {
        var path=FilePath;if(!File.Exists(path))return null;
        try
        {
            var value=JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
            if(value is null||!string.Equals(value.MaMay,Environment.MachineName,StringComparison.OrdinalIgnoreCase))return null;
            var bytes=Protect(Convert.FromBase64String(value.DuLieuDaBaoVe),true);
            try{return new(value.MaMay,Encoding.UTF8.GetString(bytes));}finally{CryptographicOperations.ZeroMemory(bytes);}
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or FormatException or CryptographicException){return null;}
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob{public int Length;public IntPtr Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr handle);
    private static byte[] Protect(byte[] bytes,bool decrypt)
    {
        var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Blob output=default;
        try
        {
            Marshal.Copy(bytes,0,input.Data,bytes.Length);
            var ok=decrypt?CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok)throw new CryptographicException("Không bảo vệ/đọc được cấu hình POS của người dùng Windows này.");
            var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;
        }
        finally
        {
            for(var i=0;i<input.Length;i++)Marshal.WriteByte(input.Data,i,0);Marshal.FreeHGlobal(input.Data);
            if(output.Data!=IntPtr.Zero){for(var i=0;i<output.Length;i++)Marshal.WriteByte(output.Data,i,0);LocalFree(output.Data);}
        }
    }
}
