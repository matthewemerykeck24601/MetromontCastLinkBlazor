// MetromontCastLink/MetromontCastLink/Services/ForgeAuthService.cs
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MetromontCastLink.Services
{
    public class ForgeAuthService : IForgeAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ForgeAuthService> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private const string FORGE_AUTH_URL = "https://developer.api.autodesk.com/authentication/v2/token";
        private const string CACHE_KEY_2LEGGED = "forge_2legged_token";

        public ForgeAuthService(
            HttpClient httpClient,
            IMemoryCache cache,
            ILogger<ForgeAuthService> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _cache = cache;
            _logger = logger;
            _clientId = configuration["ACC:ClientId"] ?? throw new InvalidOperationException("ACC ClientId not configured");
            _clientSecret = configuration["ACC:ClientSecret"] ?? throw new InvalidOperationException("ACC ClientSecret not configured");
        }

        public async Task<string?> Get2LeggedTokenAsync()
        {
            try
            {
                // Check cache first
                if (_cache.TryGetValue(CACHE_KEY_2LEGGED, out string? cachedToken))
                {
                    _logger.LogInformation("Using cached 2-legged token");
                    return cachedToken;
                }

                _logger.LogInformation("Requesting new 2-legged token from Autodesk");

                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "bucket:create bucket:read bucket:update bucket:delete data:read data:write data:create"),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret)
                });

                var request = new HttpRequestMessage(HttpMethod.Post, FORGE_AUTH_URL)
                {
                    Content = requestBody
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get 2-legged token: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                if (tokenData?.AccessToken == null)
                {
                    _logger.LogError("No access token in response");
                    return null;
                }

                // Cache the token with expiration
                var expiresIn = tokenData.ExpiresIn > 0 ? tokenData.ExpiresIn : 3600;
                var cacheExpiration = TimeSpan.FromSeconds(expiresIn - 300); // Expire 5 minutes early

                _cache.Set(CACHE_KEY_2LEGGED, tokenData.AccessToken, cacheExpiration);

                _logger.LogInformation($"Successfully obtained 2-legged token, expires in {expiresIn} seconds");
                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting 2-legged token");
                return null;
            }
        }

        public async Task<string?> Get3LeggedTokenAsync(string authorizationCode)
        {
            try
            {
                _logger.LogInformation("Exchanging authorization code for 3-legged token");

                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", authorizationCode),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", "https://localhost:7050/signin-acc")
                });

                var request = new HttpRequestMessage(HttpMethod.Post, FORGE_AUTH_URL)
                {
                    Content = requestBody
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get 3-legged token: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                if (tokenData?.AccessToken == null)
                {
                    _logger.LogError("No access token in response");
                    return null;
                }

                _logger.LogInformation("Successfully obtained 3-legged token");
                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting 3-legged token");
                return null;
            }
        }

        public async Task<string?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Refreshing access token");

                var requestBody = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("client_secret", _clientSecret)
                });

                var request = new HttpRequestMessage(HttpMethod.Post, FORGE_AUTH_URL)
                {
                    Content = requestBody
                };
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to refresh token: {response.StatusCode} - {errorContent}");
                    return null;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent);

                if (tokenData?.AccessToken == null)
                {
                    _logger.LogError("No access token in refresh response");
                    return null;
                }

                _logger.LogInformation("Successfully refreshed token");
                return tokenData.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return null;
            }
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string? TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }
    }
}