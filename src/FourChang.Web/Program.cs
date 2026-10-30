using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider(); // UI preview only: no persistent identity keys.
var app = builder.Build();
app.UseStaticFiles();
var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
if (Directory.Exists(assets)) app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assets), RequestPath = "/assets" });
app.MapRazorPages();
app.Run();

