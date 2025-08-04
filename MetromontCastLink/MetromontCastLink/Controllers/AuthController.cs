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

        [HttpPost("callback")]
        public async Task<IActionResult> ExchangeCodeForToken([FromBody] TokenExchangeRequest request)
        {
            try
            {
                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    _logger.LogError("Missing ACC CLIENT_ID or CLIENT_SECRET");
                    return StatusCode(500, new
                    {
                        error = "Server configuration error",
                        details = "Missing CLIENT_ID or CLIENT_SECRET"
                    });
                }

                _logger.LogInformation("Exchanging code for token...");

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
                var response = await httpClient.PostAsync(
                    "https://developer.api.autodesk.com/authentication/v2/token",
                    tokenRequestBody);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Autodesk response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Token exchange failed: {responseContent}");
                    return StatusCode((int)response.StatusCode, new
                    {
                        error = "Token exchange failed",
                        details = responseContent
                    });
                }

                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (tokenData?.AccessToken == null)
                {
                    return BadRequest(new { error = "Invalid token response" });
                }

                // Get user profile from ACC
                var userProfile = await GetUserProfile(tokenData.AccessToken);

                // Generate internal JWT token
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token exchange");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                var clientId = _configuration["ACC:ClientId"];
                var clientSecret = _configuration["ACC:ClientSecret"];

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
                        ProfileImage = data.GetProperty("profileImages").GetProperty("sizeX80").GetString()
                    };
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
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
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