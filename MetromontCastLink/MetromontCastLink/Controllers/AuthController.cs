// MetromontCastLink/MetromontCastLink/Controllers/AuthController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MetromontCastLink.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("test")]
        public IActionResult TestAuth()
        {
            // Enhanced test endpoint with more debugging info
            var configRoot = _configuration as IConfigurationRoot;
            var providers = new List<string>();

            if (configRoot != null)
            {
                foreach (var provider in configRoot.Providers)
                {
                    providers.Add(provider.GetType().Name);
                }
            }

            return Ok(new
            {
                status = "Auth controller is working",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                clientIdConfigured = !string.IsNullOrEmpty(_configuration["ACC:ClientId"]),
                clientSecretConfigured = !string.IsNullOrEmpty(_configuration["ACC:ClientSecret"]),
                jwtKeyConfigured = !string.IsNullOrEmpty(_configuration["Jwt:Key"]),
                configurationProviders = providers,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("callback")]
        public async Task<IActionResult> ExchangeCodeForToken([FromBody] TokenExchangeRequest request)
        {
            try
            {
                // Enhanced debug configuration
                _logger.LogInformation("=== Configuration Debug ===");
                _logger.LogInformation($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
                _logger.LogInformation($"UserProfile: {Environment.GetEnvironmentVariable("USERPROFILE")}");
                _logger.LogInformation($"ContentRoot: {Directory.GetCurrentDirectory()}");

                // Check all configuration sources
                var configRoot = _configuration as IConfigurationRoot;
                if (configRoot != null)
                {
                    _logger.LogInformation("Configuration Providers:");
                    foreach (var provider in configRoot.Providers)
                    {
                        _logger.LogInformation($"  - {provider.GetType().Name}");

                        // Check if this provider has our keys
                        if (provider.TryGet("ACC:ClientId", out var clientIdValue))
                        {
                            _logger.LogInformation($"    Found ACC:ClientId in {provider.GetType().Name}");
                        }
                        if (provider.TryGet("ACC:ClientSecret", out var secretValue))
                        {
                            _logger.LogInformation($"    Found ACC:ClientSecret in {provider.GetType().Name}: {(string.IsNullOrEmpty(secretValue) ? "EMPTY" : "SET (length: " + secretValue.Length + ")")}");
                        }
                        if (provider.TryGet("Jwt:Key", out var jwtValue))
                        {
                            _logger.LogInformation($"    Found Jwt:Key in {provider.GetType().Name}");
                        }
                    }
                }

                // Try different ways to get the configuration values
                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];
                var jwtKey = _configuration["Jwt:Key"];

                _logger.LogInformation($"ACC:ClientId: {(string.IsNullOrEmpty(clientId) ? "NOT FOUND" : "Found (starts with: " + clientId.Substring(0, Math.Min(8, clientId.Length)) + "...)")}");
                _logger.LogInformation($"ACC:ClientSecret: {(string.IsNullOrEmpty(clientSecret) ? "NOT FOUND" : "Found (length: " + clientSecret.Length + ")")}");
                _logger.LogInformation($"Jwt:Key: {(string.IsNullOrEmpty(jwtKey) ? "NOT FOUND" : "Found")}");

                // Check user secrets specifically
                var userSecretsId = configRoot?.GetDebugView();
                if (!string.IsNullOrEmpty(userSecretsId) && userSecretsId.Contains("Secrets"))
                {
                    _logger.LogInformation("User Secrets appear to be configured");
                }

                _logger.LogInformation("=== End Configuration Debug ===");

                // Validate required configuration
                if (string.IsNullOrEmpty(clientId))
                {
                    _logger.LogError("ACC ClientId is not configured");
                    return StatusCode(500, new
                    {
                        error = "Server configuration error",
                        details = "ACC ClientId is not configured. Please check your configuration."
                    });
                }

                if (string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("ACC ClientSecret is not configured");
                    return StatusCode(500, new
                    {
                        error = "Server configuration error",
                        details = "ACC ClientSecret is not configured. Please add it to user secrets using: dotnet user-secrets set \"ACC:ClientSecret\" \"your-secret-here\""
                    });
                }

                _logger.LogInformation($"Exchanging code for token - ClientId: {clientId[..8]}...");

                // Create the token request
                var tokenRequestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", request.Code),
                    new KeyValuePair<string, string>("redirect_uri", request.RedirectUri),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await httpClient.PostAsync(
                    "https://developer.api.autodesk.com/authentication/v2/token",
                    tokenRequestBody);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Autodesk response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Token exchange failed: {responseContent}");

                    // Parse error response for better error messages
                    try
                    {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        var errorMessage = errorData.GetProperty("error").GetString();
                        var errorDescription = errorData.TryGetProperty("error_description", out var desc)
                            ? desc.GetString() : "No additional details provided";

                        return StatusCode((int)response.StatusCode, new
                        {
                            error = errorMessage,
                            details = errorDescription,
                            hint = errorMessage == "invalid_client"
                                ? "Check that your ACC app credentials are correct and the app is approved"
                                : "Check the authorization code and redirect URI"
                        });
                    }
                    catch
                    {
                        return StatusCode((int)response.StatusCode, new
                        {
                            error = "Token exchange failed",
                            details = responseContent
                        });
                    }
                }

                // Parse the successful response
                var tokenData = JsonSerializer.Deserialize<AutodeskTokenResponse>(responseContent);
                if (tokenData == null)
                {
                    _logger.LogError("Failed to parse token response");
                    return StatusCode(500, new { error = "Failed to parse token response" });
                }

                _logger.LogInformation($"Successfully obtained Autodesk token. Expires in: {tokenData.ExpiresIn} seconds");

                // Generate internal JWT token
                var jwtToken = GenerateJwtToken(tokenData.AccessToken);

                return Ok(new
                {
                    token = jwtToken,
                    autodeskToken = tokenData.AccessToken,
                    expiresIn = tokenData.ExpiresIn,
                    tokenType = tokenData.TokenType,
                    refreshToken = tokenData.RefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token exchange");
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    details = ex.Message,
                    type = ex.GetType().Name
                });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    return StatusCode(500, new { error = "Server configuration error" });
                }

                var tokenRequestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", request.RefreshToken),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(
                    "https://developer.api.autodesk.com/authentication/v2/token",
                    tokenRequestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Token refresh failed: {error}");
                    return StatusCode((int)response.StatusCode, new { error = "Token refresh failed" });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<AutodeskTokenResponse>(responseContent);

                if (tokenData == null)
                {
                    return StatusCode(500, new { error = "Failed to parse token response" });
                }

                // Generate new internal JWT token
                var jwtToken = GenerateJwtToken(tokenData.AccessToken);

                return Ok(new
                {
                    token = jwtToken,
                    autodeskToken = tokenData.AccessToken,
                    expiresIn = tokenData.ExpiresIn,
                    tokenType = tokenData.TokenType,
                    refreshToken = tokenData.RefreshToken
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpGet("userinfo")]
        public async Task<IActionResult> GetUserInfo()
        {
            try
            {
                // Extract Autodesk token from JWT
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { error = "No authorization header" });
                }

                var jwtToken = authHeader.Substring("Bearer ".Length);
                var autodeskToken = ExtractAutodeskTokenFromJwt(jwtToken);

                if (string.IsNullOrEmpty(autodeskToken))
                {
                    return Unauthorized(new { error = "Invalid token" });
                }

                // Call Autodesk userinfo endpoint
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", autodeskToken);

                var response = await httpClient.GetAsync("https://api.userprofile.autodesk.com/userinfo");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get user info: {error}");
                    return StatusCode((int)response.StatusCode, new { error = "Failed to get user info" });
                }

                var userInfo = await response.Content.ReadAsStringAsync();
                return Ok(JsonSerializer.Deserialize<JsonElement>(userInfo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user info");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private string GenerateJwtToken(string autodeskToken)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                throw new InvalidOperationException("JWT Key is not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("autodeskToken", autodeskToken),
                new Claim(ClaimTypes.Name, "ACC User"),
                new Claim("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "MetromontCastLink",
                audience: _configuration["Jwt:Audience"] ?? "MetromontCastLinkUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string? ExtractAutodeskTokenFromJwt(string jwtToken)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadJwtToken(jwtToken);
                var autodeskTokenClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "autodeskToken");
                return autodeskTokenClaim?.Value;
            }
            catch
            {
                return null;
            }
        }
    }

    // Request/Response models
    public class TokenExchangeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("redirectUri")]
        public string RedirectUri { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AutodeskTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}