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
            return Ok(new
            {
                status = "Auth controller is working",
                clientIdConfigured = !string.IsNullOrEmpty(_configuration["ACC:ClientId"]),
                clientSecretConfigured = !string.IsNullOrEmpty(_configuration["ACC:ClientSecret"]),
                timestamp = DateTime.UtcNow
            });
        }

        [HttpPost("callback")]
        public async Task<IActionResult> ExchangeCodeForToken([FromBody] TokenExchangeRequest request)
        {
            try
            {
                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

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
                        details = "ACC ClientSecret is not configured. Please add it to user secrets."
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
                                ? "Check your ACC ClientId and ClientSecret in user secrets"
                                : "Check your authorization code and redirect URI"
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

                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (tokenData?.AccessToken == null)
                {
                    return BadRequest(new { error = "Invalid token response - no access token received" });
                }

                _logger.LogInformation("Successfully obtained ACC access token");

                // Get user profile from ACC
                var userProfile = await GetUserProfile(tokenData.AccessToken);

                // Generate internal JWT token
                var jwtKey = _configuration["Jwt:Key"];
                if (string.IsNullOrEmpty(jwtKey))
                {
                    _logger.LogError("JWT Key is not configured");
                    return StatusCode(500, new { error = "JWT signing key not configured" });
                }

                var jwtToken = GenerateJwtToken(
                    userProfile?.UserId ?? "unknown",
                    userProfile?.Email ?? "unknown@example.com",
                    userProfile?.Name ?? "Unknown User"
                );

                // Return both ACC tokens and internal JWT
                return Ok(new AuthResponse
                {
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken,
                    ExpiresIn = tokenData.ExpiresIn,
                    Scope = tokenData.Scope,
                    JwtToken = jwtToken,
                    UserProfile = userProfile
                });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error during token exchange");
                return StatusCode(503, new
                {
                    error = "Network error",
                    details = "Unable to reach Autodesk authentication service. Check your internet connection."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token exchange");
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    details = ex.Message
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

                var refreshRequestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", request.RefreshToken),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret)
                });

                var httpClient = _httpClientFactory.CreateClient();
                var response = await httpClient.PostAsync(
                    "https://developer.api.autodesk.com/authentication/v2/token",
                    refreshRequestBody);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Token refresh failed: {errorContent}");
                    return StatusCode((int)response.StatusCode, new { error = "Token refresh failed" });
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Get user profile again if needed
                var userProfile = await GetUserProfile(tokenData.AccessToken);

                // Generate new JWT
                var jwtToken = GenerateJwtToken(
                    userProfile?.UserId ?? "unknown",
                    userProfile?.Email ?? "unknown@example.com",
                    userProfile?.Name ?? "Unknown User"
                );

                return Ok(new AuthResponse
                {
                    AccessToken = tokenData.AccessToken,
                    RefreshToken = tokenData.RefreshToken,
                    ExpiresIn = tokenData.ExpiresIn,
                    Scope = tokenData.Scope,
                    JwtToken = jwtToken,
                    UserProfile = userProfile
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private async Task<UserProfile?> GetUserProfile(string accessToken)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var request = new HttpRequestMessage(HttpMethod.Get,
                    "https://developer.api.autodesk.com/userprofile/v1/users/@me");
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<JsonElement>(content);

                    return new UserProfile
                    {
                        UserId = data.GetProperty("userId").GetString() ?? "",
                        Name = $"{data.GetProperty("firstName").GetString()} {data.GetProperty("lastName").GetString()}",
                        Email = data.GetProperty("emailId").GetString() ?? "",
                        ProfileImage = data.TryGetProperty("profileImages", out var images) &&
                                     images.TryGetProperty("sizeX80", out var img)
                                     ? img.GetString() : null
                    };
                }
                else
                {
                    _logger.LogWarning($"Failed to get user profile: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
            }
            return null;
        }

        private string GenerateJwtToken(string userId, string email, string name)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Email, email),
                    new Claim(ClaimTypes.Name, name),
                    new Claim("acc_authenticated", "true"),
                    new Claim("authenticated_at", DateTime.UtcNow.ToString("O"))
                }),
                Expires = DateTime.UtcNow.AddDays(1),
                Issuer = _configuration["Jwt:Issuer"] ?? "MetromontCastLink",
                Audience = _configuration["Jwt:Audience"] ?? "MetromontCastLinkUsers",
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public class TokenExchangeRequest
    {
        public string Code { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public int ExpiresIn { get; set; }
        public string? Scope { get; set; }
        public string JwtToken { get; set; } = string.Empty;
        public UserProfile? UserProfile { get; set; }
    }

    public class UserProfile
    {
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? ProfileImage { get; set; }
    }
}