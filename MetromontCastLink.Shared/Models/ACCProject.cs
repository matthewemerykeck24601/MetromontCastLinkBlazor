using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    public class ACCProject
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string HubId { get; set; } = "";  // Stores the hub this project belongs to
        public string Number { get; set; } = "";
        public string Location { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Dictionary<string, object> Attributes { get; set; } = new();
    }
}