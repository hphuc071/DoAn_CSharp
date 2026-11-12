using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FourChang.Presentation.Shared.ApiClients;
using FourChang.Presentation.Shared.Contracts.Customer;
using FourChang.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Checkout=FourChang.Web.Pages.Checkout.IndexModel;

sealed record BrowserState(Dictionary<string,string> Cookies,string Csrf,string Proof);
static partial class ConsentRun
{
 static async Task<WebApplication> WebAsync(WebApplication api) {
  var b=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(Checkout).Assembly.GetName().Name,
   ContentRootPath=Path.GetFullPath("src/Presentation/FourChang.Web"),EnvironmentName="IsolatedSqlFixture"});
  b.Configuration.Sources.Clear();b.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["Checkout:OrderCreationEnabled"]="true"});b.Logging.ClearProviders();b.WebHost.UseTestServer();
  b.Services.AddRazorPages();b.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
  b.Services.AddSingleton<IPaymentQrSource,UnavailablePaymentQrSource>();
  b.Services.AddHttpClient<CafeApiClient>(h=>h.BaseAddress=new Uri("http://consent-api.invalid"))
   .ConfigurePrimaryHttpMessageHandler(()=>api.GetTestServer().CreateHandler());
  var app=b.Build();app.MapRazorPages();await app.StartAsync();return app;
 }
 static void Cookies(HttpResponseMessage response,Dictionary<string,string> jar) {
  if(response.Headers.TryGetValues("Set-Cookie",out var all))foreach(var c in all){var part=c.Split(';')[0];var equal=part.IndexOf('=');jar[part[..equal]]=part[(equal+1)..];}
 }
 static string Csrf(string html)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
 static void SetCookies(HttpClient client,Dictionary<string,string> jar){client.DefaultRequestHeaders.Remove("Cookie");client.DefaultRequestHeaders.Add("Cookie",string.Join("; ",jar.Select(x=>x.Key+"="+x.Value)));}
 static async Task<BrowserState> WebLoginAsync(HttpClient browser) {
  var jar=new Dictionary<string,string>();var get=await browser.GetAsync("/Account/Login");Cookies(get,jar);SetCookies(browser,jar);var token=Csrf(await get.Content.ReadAsStringAsync());Check(token.Length>0,"Razor real login CSRF issued");
  var post=await browser.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{["TenDangNhap"]="UC06_CUSTOMER_A",["MatKhau"]=Environment.GetEnvironmentVariable("DINEIN_CUSTOMER_PASSWORD")!,["MaThaoTac"]=Guid.NewGuid().ToString(),["__RequestVerificationToken"]=token}));
  Check(post.StatusCode==HttpStatusCode.Found,"Razor real Customer login302");Cookies(post,jar);SetCookies(browser,jar);
  Check(post.Headers.GetValues("Set-Cookie").Any(c=>c.StartsWith("fourchang_session=")&&c.Contains("httponly",StringComparison.OrdinalIgnoreCase)),"Real Customer cookie HttpOnly");
  var checkout=await browser.GetAsync("/Checkout");Check(checkout.StatusCode==HttpStatusCode.OK,"Real owned Checkout GET200");Cookies(checkout,jar);SetCookies(browser,jar);var html=await checkout.Content.ReadAsStringAsync();
  var proof=WebUtility.HtmlDecode(Regex.Match(html,"data-owner-proof=\"([^\"]+)\"").Groups[1].Value);token=Csrf(html);Check(proof.Length>0&&token.Length>0,"Checkout ownerProof and CSRF from real session");
  return new(jar,token,proof);
 }
 static async Task<(HttpResponseMessage Response,string Body)> WebCreateAsync(HttpClient browser,BrowserState state,CreateCustomerOrderRequest payload,Guid operation,decimal total,bool csrf) {
  using var request=new HttpRequestMessage(HttpMethod.Post,"/Checkout?handler=Create"){Content=JsonContent.Create(new Checkout.CheckoutCommand(operation,total,payload,state.Proof))};
  if(csrf)request.Headers.Add("RequestVerificationToken",state.Csrf);var response=await browser.SendAsync(request);return(response,await response.Content.ReadAsStringAsync());
 }
}
