// MetromontCastLink.Client/Services/ACCService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using MetromontCastLink.Shared.Models;

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
            var authUrl = BuildAuthorizationUrl();
            await _jsRuntime.InvokeVoidAsync("window.location.href", authUrl);
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
                // Exchange the authorization code for an access token
                var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/callback");
                tokenRequest.Content = JsonContent.Create(new
                {
                    code = code,
                    redirect_uri = _callbackUrl
                });

                var response = await _httpClient.SendAsync(tokenRequest);
                if (response.IsSuccessStatusCode)
                {
                    var tokenData = await response.Content.ReadFromJsonAsync<TokenResponse>();
                    if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                    {
                        _accessToken = tokenData.AccessToken;
                        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);

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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling callback: {ex.Message}");
            }
        }

        private void LogScopeAnalysis(string? grantedScopes)
        {
            if (string.IsNullOrEmpty(grantedScopes)) return;

            var hasDataWrite = grantedScopes.Contains("data:write");
            var hasDataCreate = grantedScopes.Contains("data:create");
            var hasBucketCreate = grantedScopes.Contains("bucket:create");
            var hasBucketRead = grantedScopes.Contains("bucket:read");
            var hasBucketUpdate = grantedScopes.Contains("bucket:update");
            var hasBucketDelete = grantedScopes.Contains("bucket:delete");

            Console.WriteLine("=== SCOPE ANALYSIS ===");
            Console.WriteLine($"Granted scopes: {grantedScopes}");
            Console.WriteLine($"Data permissions: Write={hasDataWrite}, Create={hasDataCreate}");
            Console.WriteLine($"Bucket permissions: Create={hasBucketCreate}, Read={hasBucketRead}, Update={hasBucketUpdate}, Delete={hasBucketDelete}");
            Console.WriteLine($"Full bucket permissions: {hasBucketCreate && hasBucketRead && hasBucketUpdate && hasBucketDelete}");
            Console.WriteLine("======================");
        }

        private string BuildAuthorizationUrl()
        {
            var scope = string.Join(" ", _scopes);
            var authUrl = $"https://developer.api.autodesk.com/authentication/v2/authorize" +
                         $"?response_type=code" +
                         $"&client_id={Uri.EscapeDataString(_clientId)}" +
                         $"&redirect_uri={Uri.EscapeDataString(_callbackUrl)}" +
                         $"&scope={Uri.EscapeDataString(scope)}";
            return authUrl;
        }

        // Rest of the implementation remains the same...
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
                var request = new HttpRequestMessage(HttpMethod.Get, "https://developer.api.autodesk.com/userprofile/v1/users/@me");
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

            try
            {
                // Get hubs first
                var hubsRequest = new HttpRequestMessage(HttpMethod.Get, "https://developer.api.autodesk.com/project/v1/hubs");
                hubsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var hubsResponse = await _httpClient.SendAsync(hubsRequest);
                if (hubsResponse.IsSuccessStatusCode)
                {
                    var hubsData = await hubsResponse.Content.ReadFromJsonAsync<ForgeApiResponse<HubData>>();
                    if (hubsData?.Data != null && hubsData.Data.Any())
                    {
                        var hubId = hubsData.Data.First().Id;

                        // Get projects for the hub
                        var projectsRequest = new HttpRequestMessage(HttpMethod.Get,
                            $"https://developer.api.autodesk.com/project/v1/hubs/{hubId}/projects");
                        projectsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                        var projectsResponse = await _httpClient.SendAsync(projectsRequest);
                        if (projectsResponse.IsSuccessStatusCode)
                        {
                            var projectsData = await projectsResponse.Content.ReadFromJsonAsync<ForgeApiResponse<ProjectData>>();
                            _projects = projectsData?.Data?.Select(p => new ACCProject
                            {
                                Id = p.Id,
                                Name = p.Attributes?.Name ?? "Unnamed Project",
                                Number = p.Attributes?.Number ?? "",
                                Location = p.Attributes?.Location ?? "",
                                Status = p.Attributes?.Status ?? "active"
                            }).ToList() ?? new List<ACCProject>();

                            return _projects;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting projects: {ex.Message}");
            }

            return new List<ACCProject>();
        }

        public async Task<ACCProject?> GetCurrentProjectAsync()
        {
            if (_currentProject != null)
            {
                return _currentProject;
            }

            // Try to get from session storage
            var storedProjectId = await _jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "currentProjectId");
            if (!string.IsNullOrEmpty(storedProjectId))
            {
                var projects = await GetProjectsAsync();
                _currentProject = projects.FirstOrDefault(p => p.Id == storedProjectId);
            }

            return _currentProject;
        }

        public async Task<bool> SetCurrentProjectAsync(string projectId)
        {
            var projects = await GetProjectsAsync();
            var project = projects.FirstOrDefault(p => p.Id == projectId);

            if (project != null)
            {
                _currentProject = project;
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "currentProjectId", projectId);
                return true;
            }

            return false;
        }

        public async Task<List<ProjectMember>> GetProjectMembersAsync(string projectId)
        {
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new List<ProjectMember>();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://developer.api.autodesk.com/project/v1/projects/{projectId}/users");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<ForgeApiResponse<MemberData>>();
                    return data?.Data?.Select(m => new ProjectMember
                    {
                        Id = m.Id,
                        Name = m.Attributes?.Name ?? "",
                        Email = m.Attributes?.Email ?? "",
                        Role = m.Attributes?.Role ?? "",
                        Company = m.Attributes?.Company ?? ""
                    }).ToList() ?? new List<ProjectMember>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting project members: {ex.Message}");
            }

            return new List<ProjectMember>();
        }

        public async Task SignOutAsync()
        {
            _accessToken = null;
            _tokenExpiry = DateTime.MinValue;
            _userProfile = null;
            _projects = null;
            _currentProject = null;

            // Clear stored data
            await _jsRuntime.InvokeVoidAsync("sessionStorage.clear");
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "acc_token_data");

            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = false,
                UserId = null
            });
        }

        // Helper methods
        private async Task<StoredToken?> GetStoredTokenAsync()
        {
            try
            {
                var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", "acc_token_data");
                if (!string.IsNullOrEmpty(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<StoredToken>(json);
                }
            }
            catch { }
            return null;
        }

        private async Task StoreTokenAsync(StoredToken token)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(token);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "acc_token_data", json);
            }
            catch { }
        }

        private bool IsTokenExpired(StoredToken token)
        {
            return token.ExpiresAt <= DateTime.UtcNow.AddMinutes(-5);
        }

        // Helper classes
        private class StoredToken
        {
            public string AccessToken { get; set; } = "";
            public DateTime ExpiresAt { get; set; }
            public string? RefreshToken { get; set; }
            public string? Scope { get; set; }
        }

        private class TokenResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = "";

            [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("scope")]
            public string? Scope { get; set; }
        }

        private class ForgeApiResponse<T>
        {
            public List<T>? Data { get; set; }
        }

        private class HubData
        {
            public string Id { get; set; } = "";
            public HubAttributes? Attributes { get; set; }
        }

        private class HubAttributes
        {
            public string? Name { get; set; }
            public string? Description { get; set; }
        }

        private class ProjectData
        {
            public string Id { get; set; } = "";
            public ProjectAttributes? Attributes { get; set; }
        }

        private class ProjectAttributes
        {
            public string? Name { get; set; }
            public string? Number { get; set; }
            public string? Location { get; set; }
            public string? Status { get; set; }
        }

        private class MemberData
        {
            public string Id { get; set; } = "";
            public MemberAttributes? Attributes { get; set; }
        }

        private class MemberAttributes
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Role { get; set; }
            public string? Company { get; set; }
        }
    }
}