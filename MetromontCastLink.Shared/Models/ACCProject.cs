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
}