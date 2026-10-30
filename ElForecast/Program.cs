using System.Globalization;
using ElForecast;
using ElForecast.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Stromligning;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var danish = CultureInfo.GetCultureInfo("da-DK");
CultureInfo.DefaultThreadCurrentCulture = danish;
CultureInfo.DefaultThreadCurrentUICulture = danish;

// The library builds absolute URLs, so this client needs no base address. The API answers
// with "Access-Control-Allow-Origin: *", so it can be called straight from the browser.
builder.Services.AddSingleton(_ => new StromligningClient(
    new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, AppInfo.UserAgent));
builder.Services.AddSingleton<PriceService>();
builder.Services.AddSingleton<CatalogService>();
builder.Services.AddScoped<SetupStore>();

await builder.Build().RunAsync();
