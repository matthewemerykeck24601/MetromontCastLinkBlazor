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

        [HttpPost("test-callback")]
        public async Task<IActionResult> TestExchangeCodeForToken([FromBody] TokenExchangeRequest request)
        {
            var debugInfo = new Dictionary<string, object>();

            try
            {
                debugInfo["requestReceived"] = true;
                debugInfo["codeProvided"] = !string.IsNullOrEmpty(request.Code);
                debugInfo["redirectUriProvided"] = !string.IsNullOrEmpty(request.RedirectUri);

                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

                debugInfo["clientIdConfigured"] = !string.IsNullOrEmpty(clientId);
                debugInfo["clientSecretConfigured"] = !string.IsNullOrEmpty(clientSecret);

                if (!string.IsNullOrEmpty(clientId))
                {
                    debugInfo["clientIdPrefix"] = clientId.Substring(0, Math.Min(8, clientId.Length));
                }

                // Test the token exchange
                var tokenRequestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", request.Code),
                    new KeyValuePair<string, string>("redirect_uri", request.RedirectUri),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                debugInfo["requestBodyCreated"] = true;

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var response = await httpClient.PostAsync(
                    "https://developer.api.autodesk.com/authentication/v2/token",
                    tokenRequestBody);

                debugInfo["autodeskResponseStatus"] = response.StatusCode.ToString();
                debugInfo["autodeskResponseSuccess"] = response.IsSuccessStatusCode;

                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var tokenData = JsonSerializer.Deserialize<JsonElement>(responseContent);

                        debugInfo["hasAccessToken"] = tokenData.TryGetProperty("access_token", out var at);
                        debugInfo["hasRefreshToken"] = tokenData.TryGetProperty("refresh_token", out var rt);
                        debugInfo["hasExpiresIn"] = tokenData.TryGetProperty("expires_in", out var ei);
                        debugInfo["hasTokenType"] = tokenData.TryGetProperty("token_type", out var tt);

                        if (debugInfo["hasAccessToken"] is bool hasToken && hasToken)
                        {
                            var accessToken = at.GetString();
                            debugInfo["accessTokenLength"] = accessToken?.Length ?? 0;

                            // Test generating JWT
                            try
                            {
                                var jwtToken = GenerateJwtToken(accessToken);
                                debugInfo["jwtGenerated"] = !string.IsNullOrEmpty(jwtToken);
                                debugInfo["jwtLength"] = jwtToken?.Length ?? 0;
                            }
                            catch (Exception jwtEx)
                            {
                                debugInfo["jwtError"] = jwtEx.Message;
                            }
                        }

                        // Return what the frontend expects
                        return Ok(new
                        {
                            debugInfo = debugInfo,
                            actualResponse = new
                            {
                                token = "test-jwt-token",
                                autodeskToken = tokenData.GetProperty("access_token").GetString(),
                                expiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                                tokenType = tokenData.GetProperty("token_type").GetString(),
                                refreshToken = tokenData.TryGetProperty("refresh_token", out var rf) ? rf.GetString() : null
                            }
                        });
                    }
                    catch (Exception parseEx)
                    {
                        debugInfo["parseError"] = parseEx.Message;
                        return Ok(new { debugInfo = debugInfo, error = "Failed to parse token response" });
                    }
                }
                else
                {
                    // Parse error response
                    try
                    {
                        var errorData = JsonSerializer.Deserialize<JsonElement>(responseContent);
                        debugInfo["errorType"] = errorData.TryGetProperty("error", out var err) ? err.GetString() : "unknown";
                        debugInfo["errorDescription"] = errorData.TryGetProperty("error_description", out var desc) ? desc.GetString() : "no description";
                    }
                    catch
                    {
                        debugInfo["rawError"] = responseContent.Length > 200 ? responseContent.Substring(0, 200) : responseContent;
                    }

                    return Ok(new { debugInfo = debugInfo, error = "Token exchange failed" });
                }
            }
            catch (Exception ex)
            {
                debugInfo["exception"] = ex.Message;
                debugInfo["exceptionType"] = ex.GetType().Name;
                return Ok(new { debugInfo = debugInfo, error = "Exception occurred" });
            }
        }

        [HttpPost("callback")]
        public async Task<IActionResult> ExchangeCodeForToken([FromBody] TokenExchangeRequest request)
        {
            try
            {
                // Enhanced debug configuration
                _logger.LogInformation("=== Configuration Debug ===");
                _logger.LogInformation($"Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");

                // Check configuration sources
                if (_configuration is IConfigurationRoot configRoot)
                {
                    _logger.LogInformation("Configuration Providers:");
                    foreach (var provider in configRoot.Providers)
                    {
                        _logger.LogInformation($"  - {provider.GetType().Name}");

                        // Check if provider has the values we need
                        if (provider.TryGet("ACC:ClientId", out var clientIdValue))
                        {
                            _logger.LogInformation($"    Found ACC:ClientId in {provider.GetType().Name}");
                        }
                        if (provider.TryGet("ACC:ClientSecret", out var clientSecretValue))
                        {
                            _logger.LogInformation($"    Found ACC:ClientSecret in {provider.GetType().Name}: {(string.IsNullOrEmpty(clientSecretValue) ? "EMPTY" : $"SET (length: {clientSecretValue.Length})")}");
                        }
                        if (provider.TryGet("Jwt:Key", out var jwtKeyValue))
                        {
                            _logger.LogInformation($"    Found Jwt:Key in {provider.GetType().Name}");
                        }
                    }
                }

                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

                _logger.LogInformation($"ACC:ClientId: {(string.IsNullOrEmpty(clientId) ? "NOT FOUND" : $"Found (starts with: {clientId.Substring(0, Math.Min(8, clientId.Length))}...)")}");
                _logger.LogInformation($"ACC:ClientSecret: {(string.IsNullOrEmpty(clientSecret) ? "NOT FOUND" : $"Found (length: {clientSecret.Length})")}");
                _logger.LogInformation($"Jwt:Key: {(string.IsNullOrEmpty(_configuration["Jwt:Key"]) ? "NOT FOUND" : "Found")}");
                _logger.LogInformation("=== End Configuration Debug ===");

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
                // Get the Autodesk token from the authorization header
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    return Unauthorized(new { error = "No authorization token provided" });
                }

                var token = authHeader.Substring("Bearer ".Length);

                // Call Autodesk's user profile endpoint
                var httpClient = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get, "https://developer.api.autodesk.com/userprofile/v1/users/@me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new { error = "Failed to get user info" });
                }

                var content = await response.Content.ReadAsStringAsync();
                return Ok(content);
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
                throw new InvalidOperationException("JWT key not configured");
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("autodeskToken", autodeskToken),
                new Claim("timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? "MetromontCastLink",
                audience: _configuration["Jwt:Audience"] ?? "MetromontCastLinkUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    // Request/Response DTOs
    public class TokenExchangeRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = "";

        [JsonPropertyName("redirectUri")]
        public string RedirectUri { get; set; } = "";
    }

    public class RefreshTokenRequest
    {
        [JsonPropertyName("refreshToken")]
        public string RefreshToken { get; set; } = "";
    }

    public class AutodeskTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = "";
    }
}