// MetromontCastLink.Shared/Services/IACCService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetromontCastLink.Shared.Models;

namespace MetromontCastLink.Shared.Services
{
    public interface IACCService
    {
        Task<bool> IsAuthenticatedAsync();
        Task InitiateAuthenticationAsync();
        Task<string?> GetAccessTokenAsync();
        Task<UserProfile?> GetUserProfileAsync();
        Task<List<ACCProject>> GetProjectsAsync();
        Task<ACCProject?> GetCurrentProjectAsync();
        Task<bool> SetCurrentProjectAsync(string projectId);
        Task<List<ProjectMember>> GetProjectMembersAsync(string projectId);
        Task SignOutAsync();
        event EventHandler<AuthenticationStateChangedEventArgs> AuthenticationStateChanged;
    }

    public class AuthenticationStateChangedEventArgs : EventArgs
    {
        public bool IsAuthenticated { get; set; }
        public string? UserId { get; set; }
    }
}