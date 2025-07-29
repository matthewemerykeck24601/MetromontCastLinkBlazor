using MetromontCastLink.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Syncfusion.Blazor;

//Register Syncfusion license https://help.syncfusion.com/common/essential-studio/licensing/how-to-generate
//Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("YOUR LICENSE KEY");
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.Services.AddSyncfusionBlazor();
await builder.Build().RunAsync();
