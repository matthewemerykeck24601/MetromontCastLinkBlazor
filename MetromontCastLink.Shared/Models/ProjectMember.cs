// MetromontCastLink.Shared/Models/ProjectMember.cs
using System;

namespace MetromontCastLink.Shared.Models
{
    public class ProjectMember
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Company { get; set; } = "";
    }
}