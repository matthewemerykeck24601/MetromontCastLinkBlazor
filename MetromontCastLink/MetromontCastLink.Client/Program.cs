using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using MetromontCastLink.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add Syncfusion Blazor
builder.Services.AddSyncfusionBlazor();

// Add HttpClient
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Add Services
builder.Services.AddScoped<IACCService, ACCService>();
builder.Services.AddScoped<IStorageService, StorageService>();

// Build and run
await builder.Build().RunAsync();