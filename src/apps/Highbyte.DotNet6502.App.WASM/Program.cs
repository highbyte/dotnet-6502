using Blazored.Modal;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Highbyte.DotNet6502.App.WASM;
using Highbyte.DotNet6502.Logging.Console;
using Blazored.LocalStorage;
using Toolbelt.Blazor.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddBlazoredModal();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddGamepadList();

builder.Logging.ClearProviders();
builder.Logging.AddDotNet6502Console();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.AspNetCore.Components.RenderTree.*", LogLevel.None);

await builder.Build().RunAsync();
