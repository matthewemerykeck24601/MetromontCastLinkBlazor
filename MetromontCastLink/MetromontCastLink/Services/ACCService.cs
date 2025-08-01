// MetromontCastLink/MetromontCastLink/Services/ACCService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MetromontCastLink.Shared.Models;
using MetromontCastLink.Shared.Services;

namespace MetromontCastLink.Services
{
    /// <summary>
    /// Server-side implementation of IACCService for SSR compatibility.
    /// This is a stub implementation that returns safe defaults during server-side rendering.
    /// The actual authentication happens on the client side.
    /// </summary>
    public class ACCService : IACCService
    {
        public event EventHandler<AuthenticationStateChangedEventArgs>? AuthenticationStateChanged;

        public Task<bool> IsAuthenticatedAsync()
        {
            // During SSR, always return false
            return Task.FromResult(false);
        }

        public Task InitiateAuthenticationAsync()
        {
            // No-op on server side - actual authentication happens on client
            return Task.CompletedTask;
        }

        public Task<string?> GetAccessTokenAsync()
        {
            // No token available during SSR
            return Task.FromResult<string?>(null);
        }

        public Task<UserProfile?> GetUserProfileAsync()
        {
            // No user profile during SSR
            return Task.FromResult<UserProfile?>(null);
        }

        public Task<List<ACCProject>> GetProjectsAsync()
        {
            // Return empty list during SSR
            return Task.FromResult(new List<ACCProject>());
        }

        public Task<ACCProject?> GetCurrentProjectAsync()
        {
            // No current project during SSR
            return Task.FromResult<ACCProject?>(null);
        }

        public Task<bool> SetCurrentProjectAsync(string projectId)
        {
            // Can't set project during SSR
            return Task.FromResult(false);
        }

        public Task<List<ProjectMember>> GetProjectMembersAsync(string projectId)
        {
            // Return empty list during SSR
            return Task.FromResult(new List<ProjectMember>());
        }

        public Task SignOutAsync()
        {
            // No-op on server side
            return Task.CompletedTask;
        }
    }
}