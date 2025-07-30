// MetromontCastLink/MetromontCastLink/Controllers/AuthController.cs
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
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
                    return BadRequest(new { error = "No access token in response" });
                }

                // Log scope analysis (matching your authentication.js)
                _logger.LogInformation("=== SCOPE ANALYSIS ===");
                _logger.LogInformation($"Granted scopes: {tokenData.Scope}");

                var scopeAnalysis = AnalyzeScopes(tokenData.Scope);
                _logger.LogInformation($"Data permissions: Write={scopeAnalysis.HasDataWrite}, Create={scopeAnalysis.HasDataCreate}");
                _logger.LogInformation($"Bucket permissions: Create={scopeAnalysis.HasBucketCreate}, Read={scopeAnalysis.HasBucketRead}, Update={scopeAnalysis.HasBucketUpdate}, Delete={scopeAnalysis.HasBucketDelete}");
                _logger.LogInformation($"Full bucket permissions: {scopeAnalysis.HasFullBucketPermissions}");
                _logger.LogInformation("======================");

                // Return the token data with scope analysis
                return Ok(new
                {
                    access_token = tokenData.AccessToken,
                    expires_in = tokenData.ExpiresIn,
                    refresh_token = tokenData.RefreshToken,
                    scope = tokenData.Scope,
                    scope_analysis = scopeAnalysis
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth function error");
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    details = ex.Message,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        private ScopeAnalysis AnalyzeScopes(string? grantedScopes)
        {
            if (string.IsNullOrEmpty(grantedScopes))
            {
                return new ScopeAnalysis();
            }

            var analysis = new ScopeAnalysis
            {
                GrantedScopes = grantedScopes,
                HasDataWrite = grantedScopes.Contains("data:write"),
                HasDataCreate = grantedScopes.Contains("data:create"),
                HasBucketCreate = grantedScopes.Contains("bucket:create"),
                HasBucketRead = grantedScopes.Contains("bucket:read"),
                HasBucketUpdate = grantedScopes.Contains("bucket:update"),
                HasBucketDelete = grantedScopes.Contains("bucket:delete")
            };

            analysis.EnhancedPermissions = analysis.HasDataWrite && analysis.HasDataCreate;
            analysis.HasFullBucketPermissions = analysis.HasBucketCreate &&
                                              analysis.HasBucketRead &&
                                              analysis.HasBucketUpdate &&
                                              analysis.HasBucketDelete;

            return analysis;
        }

        public class TokenExchangeRequest
        {
            public string Code { get; set; } = "";
            public string RedirectUri { get; set; } = "";
        }

        public class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = "";

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }

        public class ScopeAnalysis
        {
            public string GrantedScopes { get; set; } = "";
            public bool HasDataWrite { get; set; }
            public bool HasDataCreate { get; set; }
            public bool HasBucketCreate { get; set; }
            public bool HasBucketRead { get; set; }
            public bool HasBucketUpdate { get; set; }
            public bool HasBucketDelete { get; set; }
            public bool EnhancedPermissions { get; set; }
            public bool HasFullBucketPermissions { get; set; }
        }
    }
}