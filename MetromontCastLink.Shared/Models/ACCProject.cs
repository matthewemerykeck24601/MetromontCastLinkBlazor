// Models/ACCProject.cs
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
}