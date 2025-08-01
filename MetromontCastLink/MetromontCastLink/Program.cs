// MetromontCastLink/MetromontCastLink/Program.cs
using MetromontCastLink.Components;
using MetromontCastLink.Services;
using MetromontCastLink.Shared.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Syncfusion.Blazor;
using System.Text;

// Register Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Mzk3MzE1OEAzMzMwMmUzMDJlMzAzYjMzMzAzYkN5b3JlZ2Q4c0hyelFiaGtWY08yWDhiYk9KamNNR21kWmxYS3ArT3JqTjA9");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// Add Syncfusion Blazor
builder.Services.AddSyncfusionBlazor();

// Add controllers for API endpoints
builder.Services.AddControllers();

// Add memory cache for token storage
builder.Services.AddMemoryCache();

// Add HttpClient
builder.Services.AddHttpClient();

// Configure HttpClient for Forge/ACC APIs
builder.Services.AddHttpClient("ForgeAPI", client =>
{
    client.BaseAddress = new Uri("https://developer.api.autodesk.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("User-Agent", "MetromontCastLink/3.0");
});

// Add authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "YourDefaultSecretKeyHere1234567890"))
    };
});

// Add authorization
builder.Services.AddAuthorization();

// Register custom services
builder.Services.AddScoped<IForgeAuthService, ForgeAuthService>();
builder.Services.AddScoped<IOssStorageService, OssStorageService>();
builder.Services.AddScoped<IACCService, ACCService>();

// Configure CORS for ACC/Forge APIs
builder.Services.AddCors(options =>
{
    options.AddPolicy("ACCPolicy", policy =>
    {
        policy.WithOrigins(
                "https://developer.api.autodesk.com",
                "https://localhost:7050",
                "https://localhost:5001"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add SignalR for real-time features (optional)
builder.Services.AddSignalR();

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.WriteIndented = true;
});

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Serve static files
app.UseStaticFiles();

// Enable routing
app.UseRouting();

// Enable CORS
app.UseCors("ACCPolicy");

// Enable authentication & authorization
app.UseAuthentication();
app.UseAuthorization();

// Enable anti-forgery
app.UseAntiforgery();

// Map Razor components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MetromontCastLink.Client._Imports).Assembly);

// Map API controllers
app.MapControllers();

// Map fallback for client-side routing
app.MapFallbackToFile("index.html");

// Optional: Add health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Optional: Add SignalR hubs if using real-time features
// app.MapHub<NotificationHub>("/hubs/notifications");

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Metromont CastLink v3.0 starting...");
logger.LogInformation($"Environment: {app.Environment.EnvironmentName}");
logger.LogInformation($"URLs: {string.Join(", ", builder.Configuration["urls"]?.Split(';') ?? new[] { "Not configured" })}");

// Run the application
app.Run();