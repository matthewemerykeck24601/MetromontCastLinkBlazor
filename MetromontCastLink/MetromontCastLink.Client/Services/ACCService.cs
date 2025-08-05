// MetromontCastLink.Client/Services/ACCService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MetromontCastLink.Shared.Models;
using Microsoft.Extensions.Configuration;
using MetromontCastLink.Shared.Services;

namespace MetromontCastLink.Client.Services
{
    public class ACCService : IACCService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;
        private readonly string _clientId;
        private readonly string _callbackUrl;
        private readonly string[] _scopes;

        private string? _accessToken;
        private DateTime _tokenExpiry;
        private UserProfile? _userProfile;
        private List<ACCProject>? _projects;
        private ACCProject? _currentProject;

        public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

        public ACCService(HttpClient httpClient, IJSRuntime jsRuntime, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
            _clientId = configuration["ACC:ClientId"] ?? throw new InvalidOperationException("ACC ClientId not configured");
            _callbackUrl = configuration["ACC:CallbackUrl"] ?? "https://localhost:7050/signin-acc";

            // These scopes match your authentication.js file
            _scopes = new[]
            {
                "data:read",
                "data:write",
                "data:create",
                "data:search",
                "account:read",
                "user:read",
                "viewables:read",
                "bucket:create",
                "bucket:read",
                "bucket:update",
                "bucket:delete"
            };

            Console.WriteLine($"ACCService initialized - ClientId: {_clientId}, CallbackUrl: {_callbackUrl}");
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            // Check if we have a valid token
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.UtcNow)
            {
                return true;
            }

            // Try to get token from session storage
            var storedToken = await GetStoredTokenAsync();
            if (storedToken != null && !IsTokenExpired(storedToken))
            {
                _accessToken = storedToken.AccessToken;
                _tokenExpiry = storedToken.ExpiresAt;
                return true;
            }

            return false;
        }

        public async Task InitiateAuthenticationAsync()
        {
            try
            {
                Console.WriteLine("InitiateAuthenticationAsync called");
                var authUrl = BuildAuthorizationUrl();
                Console.WriteLine($"Redirecting to: {authUrl}");

                // Try multiple methods to ensure redirect works
                try
                {
                    // Method 1: Direct window.location.href
                    await _jsRuntime.InvokeVoidAsync("eval", $"window.location.href = '{authUrl}'");
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"Method 1 failed: {ex1.Message}, trying method 2");
                    try
                    {
                        // Method 2: window.location.replace
                        await _jsRuntime.InvokeVoidAsync("eval", $"window.location.replace('{authUrl}')");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"Method 2 failed: {ex2.Message}, trying method 3");
                        // Method 3: window.open
                        await _jsRuntime.InvokeVoidAsync("eval", $"window.open('{authUrl}', '_self')");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in InitiateAuthenticationAsync: {ex.Message}");
                throw;
            }
        }

        private string BuildAuthorizationUrl()
        {
            var scope = string.Join(" ", _scopes);
            var authUrl = $"https://developer.api.autodesk.com/authentication/v2/authorize" +
                         $"?response_type=code" +
                         $"&client_id={Uri.EscapeDataString(_clientId)}" +
                         $"&redirect_uri={Uri.EscapeDataString(_callbackUrl)}" +
                         $"&scope={Uri.EscapeDataString(scope)}";

            Console.WriteLine($"Built auth URL: {authUrl}");
            Console.WriteLine($"Client ID: {_clientId}");
            Console.WriteLine($"Callback URL: {_callbackUrl}");
            Console.WriteLine($"Scopes: {scope}");

            return authUrl;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            if (await IsAuthenticatedAsync())
            {
                return _accessToken;
            }
            return null;
        }

        public async Task HandleCallbackAsync(string code)
        {
            try
            {
                Console.WriteLine($"HandleCallbackAsync called with code: {code}");

                // Exchange the authorization code for an access token
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/callback");
                tokenRequest.Content = JsonContent.Create(new
                {
                    code = code,
                    redirectUri = _callbackUrl  // FIXED: Changed from redirecturi to redirectUri (camelCase)
                });

                Console.WriteLine($"Sending token request with redirectUri: {_callbackUrl}");

                var response = await _httpClient.SendAsync(tokenRequest);

                if (response.IsSuccessStatusCode)
                {
                    var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>();
                    if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                    {
                        _accessToken = tokenData.AccessToken;
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

                        Console.WriteLine($"Token exchange successful, expires in {tokenData.ExpiresIn} seconds");

                        // Store token in session storage
                        await StoreTokenAsync(new StoredToken
                        {
                            AccessToken = tokenData.AccessToken,
                            ExpiresAt = _tokenExpiry,
                            RefreshToken = tokenData.RefreshToken,
                            Scope = tokenData.Scope
                        });

                        // Log scope analysis
                        LogScopeAnalysis(tokenData.Scope);

                        // Notify listeners
                        AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
                        {
                            IsAuthenticated = true,
                            UserId = null
                        });
                    }
                    else
                    {
                        Console.WriteLine("Token response was successful but no access token received");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Token exchange failed: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling callback: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        private void LogScopeAnalysis(string? scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                Console.WriteLine("No scope information received");
                return;
            }

            Console.WriteLine($"Granted scopes: {scope}");
            var grantedScopes = scope.Split(' ');
            var missingScopes = _scopes.Except(grantedScopes).ToList();

            if (missingScopes.Any())
            {
                Console.WriteLine($"Warning: The following requested scopes were not granted: {string.Join(", ", missingScopes)}");
            }
        }

        public async Task<UserProfile?> GetUserProfileAsync()
        {
            if (_userProfile != null)
            {
                return _userProfile;
            }

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/userinfo");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<dynamic>();
                    _userProfile = new UserProfile
                    {
                        Id = data.userId?.ToString() ?? "",
                        Name = $"{data.firstName} {data.lastName}",
                        Email = data.emailId?.ToString() ?? "",
                        Role = data.jobTitle?.ToString() ?? ""
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user profile: {ex.Message}");
            }

            return _userProfile;
        }

        public async Task<List<ACCProject>> GetProjectsAsync()
        {
            if (_projects != null)
            {
                return _projects;
            }

            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new List<ACCProject>();
            }

            // TODO: Implement actual project retrieval
            _projects = new List<ACCProject>();
            return _projects;
        }

        public Task<ACCProject?> GetCurrentProjectAsync()
        {
            return Task.FromResult(_currentProject);
        }

        public Task<bool> SetCurrentProjectAsync(string projectId)
        {
            _currentProject = _projects?.FirstOrDefault(p => p.Id == projectId);
            return Task.FromResult(_currentProject != null);
        }

        public Task<List<ProjectMember>> GetProjectMembersAsync(string projectId)
        {
            // TODO: Implement actual member retrieval
            return Task.FromResult(new List<ProjectMember>());
        }

        public async Task SignOutAsync()
        {
            _accessToken = null;
            _tokenExpiry = DateTime.MinValue;
            _userProfile = null;
            _projects = null;
            _currentProject = null;

            // Clear stored token
            await ClearStoredTokenAsync();

            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = false,
                UserId = null
            });
        }

        // Helper methods for token storage
        private async Task<StoredToken?> GetStoredTokenAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "acc_token");
                if (!string.IsNullOrEmpty(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<StoredToken>(json);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stored token: {ex.Message}");
            }
            return null;
        }

        private async Task StoreTokenAsync(StoredToken token)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(token);
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "acc_token", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing token: {ex.Message}");
            }
        }

        private async Task ClearStoredTokenAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "acc_token");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing stored token: {ex.Message}");
            }
        }

        private bool IsTokenExpired(StoredToken token)
        {
            return token.ExpiresAt <= DateTime.UtcNow.AddMinutes(-5); // 5 minute buffer
        }

        // Internal classes
        private class StoredToken
        {
            public string AccessToken { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public string? RefreshToken { get; set; }
            public string? Scope { get; set; }
        }

        private class TokenResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public string? RefreshToken { get; set; }
            public int ExpiresIn { get; set; }
            public string? Scope { get; set; }
            public string? TokenType { get; set; }
        }
    }
}