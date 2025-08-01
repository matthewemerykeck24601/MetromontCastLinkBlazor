﻿using MetromontCastLink.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;
using MetromontCastLink.Shared.Services;


// Register Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mzk3MzE1OEAzMzMwMmUzMDJlMzAzYjMzMzAzYkN5b3JlZ2Q4c0hyelFiaGtWY08yWDhiYk9KamNNR21kWmxYS3ArT3JqTjA9");

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