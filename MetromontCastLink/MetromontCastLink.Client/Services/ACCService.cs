using MetromontCastLink.Shared.Models;
using MetromontCastLink.Shared.Services;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        private string? _jwtToken;  // Store the internal JWT token as well
        private DateTime _tokenExpiry;
        private UserProfile? _userProfile;
        private List<ACCProject>? _projects;
        private ACCProject? _currentProject;

        // PKCE parameters
        private string? _codeVerifier;
        private string? _codeChallenge;

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
                _jwtToken = storedToken.JwtToken;
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

                // Generate PKCE parameters
                GeneratePKCEParameters();

                // Store code verifier in session storage for later use
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "code_verifier", _codeVerifier);

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

        private void GeneratePKCEParameters()
        {
            // Generate code verifier (43-128 characters)
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            _codeVerifier = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Generate code challenge (SHA256 hash of verifier)
            using (var sha256 = SHA256.Create())
            {
                var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(_codeVerifier));
                _codeChallenge = Convert.ToBase64String(challengeBytes)
                    .TrimEnd('=')
                    .Replace('+', '-')
                    .Replace('/', '_');
            }

            Console.WriteLine($"Generated PKCE - Verifier length: {_codeVerifier.Length}, Challenge: {_codeChallenge}");
        }

        private string BuildAuthorizationUrl()
        {
            var scope = string.Join(" ", _scopes);
            var authUrl = $"https://developer.api.autodesk.com/authentication/v2/authorize" +
                         $"?response_type=code" +
                         $"&client_id={Uri.EscapeDataString(_clientId)}" +
                         $"&redirect_uri={Uri.EscapeDataString(_callbackUrl)}" +
                         $"&scope={Uri.EscapeDataString(scope)}" +
                         $"&code_challenge={Uri.EscapeDataString(_codeChallenge!)}" +
                         $"&code_challenge_method=S256";

            Console.WriteLine($"Built auth URL with PKCE");
            Console.WriteLine($"Client ID: {_clientId}");
            Console.WriteLine($"Callback URL: {_callbackUrl}");
            Console.WriteLine($"Scopes: {scope}");
            Console.WriteLine($"Code Challenge: {_codeChallenge}");

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

                // Retrieve the code verifier from session storage
                var storedVerifier = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "code_verifier");
                if (string.IsNullOrEmpty(storedVerifier))
                {
                    Console.WriteLine("Warning: No code verifier found in session storage, using current one");
                    storedVerifier = _codeVerifier;
                }

                Console.WriteLine($"Using code verifier: {storedVerifier?.Substring(0, Math.Min(10, storedVerifier?.Length ?? 0))}... (length: {storedVerifier?.Length})");

                // Exchange the authorization code for an access token
                var tokenRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", _clientId),
                    new KeyValuePair<string, string>("redirect_uri", _callbackUrl),
                    new KeyValuePair<string, string>("code_verifier", storedVerifier!)
                });

                var tokenResponse = await _httpClient.PostAsync("https://developer.api.autodesk.com/authentication/v2/token", tokenRequest);

                if (tokenResponse.IsSuccessStatusCode)
                {
                    var tokenJson = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();

                    _accessToken = tokenJson.GetProperty("access_token").GetString();
                    var expiresIn = tokenJson.GetProperty("expires_in").GetInt32();
                    _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn);

                    // Clear the code verifier from session storage
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "code_verifier");

                    // Store token info
                    await StoreTokenAsync(_accessToken!, _tokenExpiry);

                    // Get JWT token from your server
                    await GetJwtTokenAsync();

                    // Load user profile
                    await GetUserProfileAsync();

                    // Load projects
                    await GetProjectsAsync();

                    // Notify authentication state changed
                    AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
                    {
                        IsAuthenticated = true,
                        UserId = _userProfile?.Id
                    });

                    Console.WriteLine("Authentication successful");
                }
                else
                {
                    var error = await tokenResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Token exchange failed: {error}");
                    throw new Exception($"Failed to exchange authorization code: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in HandleCallbackAsync: {ex.Message}");
                throw;
            }
        }

        private async Task GetJwtTokenAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("/api/auth/token", new StringContent(""));
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    _jwtToken = result.GetProperty("token").GetString();
                    await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "jwtToken", _jwtToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting JWT token: {ex.Message}");
            }
        }

        public async Task<UserProfile?> GetUserProfileAsync()
        {
            if (!await IsAuthenticatedAsync())
                return null;

            if (_userProfile != null)
                return _userProfile;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://developer.api.autodesk.com/userprofile/v1/users/@me");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var userData = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

                    _userProfile = new UserProfile
                    {
                        Id = userData.TryGetProperty("userId", out var userId) ? userId.GetString() ?? "" : "",
                        Name = userData.TryGetProperty("userName", out var userName) ? userName.GetString() ?? "" : "",
                        Email = userData.TryGetProperty("emailId", out var emailId) ? emailId.GetString() ?? "" : "",
                        Role = userData.TryGetProperty("jobTitle", out var jobTitle) ? jobTitle.GetString() ?? "" : ""
                    };

                    Console.WriteLine($"User profile loaded: {_userProfile.Name} ({_userProfile.Email})");
                }
                else
                {
                    Console.WriteLine($"Failed to get user profile: {response.StatusCode}");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error response: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting user profile: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return _userProfile;
        }

        public async Task<List<ACCProject>> GetProjectsAsync()
        {
            if (!await IsAuthenticatedAsync())
                return new List<ACCProject>();

            if (_projects != null)
                return _projects;

            try
            {
                // Get hubs first
                var hubsRequest = new HttpRequestMessage(HttpMethod.Get, "https://developer.api.autodesk.com/project/v1/hubs");
                hubsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                var hubsResponse = await _httpClient.SendAsync(hubsRequest);
                if (hubsResponse.IsSuccessStatusCode)
                {
                    var hubsJson = await hubsResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                    _projects = new List<ACCProject>();

                    // Check if data property exists and is an array
                    if (hubsJson.TryGetProperty("data", out var hubsData) && hubsData.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        // For each hub, get projects
                        foreach (var hub in hubsData.EnumerateArray())
                        {
                            if (!hub.TryGetProperty("id", out var hubIdProp)) continue;
                            var hubId = hubIdProp.GetString();
                            if (string.IsNullOrEmpty(hubId)) continue;

                            Console.WriteLine($"Getting projects for hub: {hubId}");

                            var projectsRequest = new HttpRequestMessage(HttpMethod.Get, $"https://developer.api.autodesk.com/project/v1/hubs/{hubId}/projects");
                            projectsRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);

                            var projectsResponse = await _httpClient.SendAsync(projectsRequest);
                            if (projectsResponse.IsSuccessStatusCode)
                            {
                                var projectsJson = await projectsResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

                                if (projectsJson.TryGetProperty("data", out var projectsData) && projectsData.ValueKind == System.Text.Json.JsonValueKind.Array)
                                {
                                    foreach (var project in projectsData.EnumerateArray())
                                    {
                                        var projectId = project.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                                        var attributes = project.TryGetProperty("attributes", out var attrProp) ? attrProp : default;

                                        var projectName = attributes.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                                                        attributes.TryGetProperty("name", out var nameProp)
                                                        ? nameProp.GetString() ?? "" : "";

                                        _projects.Add(new ACCProject
                                        {
                                            Id = projectId,
                                            Name = projectName,
                                            HubId = hubId,      // STORE THE HUB ID HERE!
                                            Number = "",        // Project number not always available
                                            Location = "",      // Not typically available in basic project info
                                            Status = "active"   // Default to active
                                        });

                                        Console.WriteLine($"Added project: {projectName} ({projectId}) in hub {hubId}");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Failed to get projects for hub {hubId}: {projectsResponse.StatusCode}");
                            }
                        }
                    }

                    Console.WriteLine($"Total projects loaded: {_projects.Count}");
                }
                else
                {
                    Console.WriteLine($"Failed to get hubs: {hubsResponse.StatusCode}");
                    var errorContent = await hubsResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error response: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting projects: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return _projects ?? new List<ACCProject>();
        }

        public async Task<ACCProject?> GetCurrentProjectAsync()
        {
            return _currentProject;
        }

        public async Task<bool> SetCurrentProjectAsync(string projectId)
        {
            if (_projects == null)
            {
                await GetProjectsAsync();
            }

            _currentProject = _projects?.FirstOrDefault(p => p.Id == projectId);

            // Store in session storage for persistence
            if (_currentProject != null)
            {
                var projectJson = JsonSerializer.Serialize(_currentProject);
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "selectedProject", projectJson);

                // Also store the hub ID separately for easy access
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "selectedHubId", _currentProject.HubId);

                return true;
            }

            return false;
        }

        public async Task<List<ProjectMember>> GetProjectMembersAsync(string projectId)
        {
            // This would typically call the ACC API to get project members
            // For now, return a mock list
            return new List<ProjectMember>
            {
                new ProjectMember { Id = "1", Name = "Project Manager", Email = "pm@metromont.com", Role = "Manager" },
                new ProjectMember { Id = "2", Name = "Engineer", Email = "engineer@metromont.com", Role = "Engineer" },
                new ProjectMember { Id = "3", Name = "QC Inspector", Email = "qc@metromont.com", Role = "Quality Control" }
            };
        }

        public async Task SignOutAsync()
        {
            // Clear tokens
            _accessToken = null;
            _jwtToken = null;
            _tokenExpiry = DateTime.MinValue;
            _userProfile = null;
            _projects = null;
            _currentProject = null;
            _codeVerifier = null;
            _codeChallenge = null;

            // Clear session storage
            await _jsRuntime.InvokeVoidAsync("sessionStorage.clear");

            // Notify authentication state changed
            AuthenticationStateChanged?.Invoke(this, new AuthenticationStateChangedEventArgs
            {
                IsAuthenticated = false
            });
        }

        // Helper methods
        private async Task<StoredToken?> GetStoredTokenAsync()
        {
            try
            {
                var tokenJson = await _jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "accToken");
                if (!string.IsNullOrEmpty(tokenJson))
                {
                    return JsonSerializer.Deserialize<StoredToken>(tokenJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting stored token: {ex.Message}");
            }
            return null;
        }

        private async Task StoreTokenAsync(string accessToken, DateTime expiresAt)
        {
            try
            {
                var token = new StoredToken
                {
                    AccessToken = accessToken,
                    JwtToken = _jwtToken ?? "",
                    ExpiresAt = expiresAt
                };
                var tokenJson = JsonSerializer.Serialize(token);
                await _jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "accToken", tokenJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error storing token: {ex.Message}");
            }
        }

        private bool IsTokenExpired(StoredToken token)
        {
            return token.ExpiresAt <= DateTime.UtcNow.AddMinutes(-5); // Refresh 5 minutes before expiry
        }

        private class StoredToken
        {
            public string AccessToken { get; set; } = "";
            public string JwtToken { get; set; } = "";
            public DateTime ExpiresAt { get; set; }
        }
    }
}