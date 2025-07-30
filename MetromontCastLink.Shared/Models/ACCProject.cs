// MetromontCastLink.Shared/Models/ACCProject.cs
using System;

namespace MetromontCastLink.Shared.Models
{
    public class ACCProject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Number { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "active";
    }

    public class ProjectMember
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Company { get; set; } = "";
    }

    public class UserProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class ACCModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Urn { get; set; } = "";
        public string Version { get; set; } = "";
        public long Size { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime LastModified { get; set; }
        public string ProjectId { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string FileType { get; set; } = "";
        public string Status { get; set; } = "";
    }
}