// MetromontCastLink.Shared/Models/BedInformation.cs
using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    public class BedInformation
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = ""; // Standard, Prestress, Specialty
        public double Length { get; set; }
        public double Width { get; set; }
        public string Location { get; set; } = "";
        public string Status { get; set; } = "Active"; // Active, Maintenance, Inactive
        public int Capacity { get; set; }
        public DateTime LastInspection { get; set; }
        public DateTime NextInspection { get; set; }
        public string Notes { get; set; } = "";
        public List<string> Features { get; set; } = new();
    }

    public class BedDatabase
    {
        private static readonly List<BedInformation> _beds = new()
        {
            new BedInformation
            {
                Id = "bed-001",
                Name = "Bed 1 - Standard",
                Type = "Standard",
                Length = 400,
                Width = 12,
                Location = "Building A",
                Status = "Active",
                Capacity = 10,
                LastInspection = DateTime.Now.AddDays(-30),
                NextInspection = DateTime.Now.AddDays(60),
                Features = new List<string> { "Steam Curing", "Automated Tensioning" }
            },
            new BedInformation
            {
                Id = "bed-002",
                Name = "Bed 2 - Prestress",
                Type = "Prestress",
                Length = 500,
                Width = 14,
                Location = "Building A",
                Status = "Active",
                Capacity = 12,
                LastInspection = DateTime.Now.AddDays(-45),
                NextInspection = DateTime.Now.AddDays(45),
                Features = new List<string> { "Self-Stressing", "Temperature Control", "Automated Tensioning" }
            },
            new BedInformation
            {
                Id = "bed-003",
                Name = "Bed 3 - Specialty",
                Type = "Specialty",
                Length = 300,
                Width = 16,
                Location = "Building B",
                Status = "Active",
                Capacity = 8,
                LastInspection = DateTime.Now.AddDays(-15),
                NextInspection = DateTime.Now.AddDays(75),
                Features = new List<string> { "Custom Forms", "Variable Width" }
            },
            new BedInformation
            {
                Id = "bed-004",
                Name = "Bed 4 - Standard",
                Type = "Standard",
                Length = 400,
                Width = 12,
                Location = "Building B",
                Status = "Maintenance",
                Capacity = 10,
                LastInspection = DateTime.Now.AddDays(-5),
                NextInspection = DateTime.Now.AddDays(10),
                Features = new List<string> { "Steam Curing" }
            },
            new BedInformation
            {
                Id = "bed-005",
                Name = "Bed 5 - Prestress",
                Type = "Prestress",
                Length = 600,
                Width = 14,
                Location = "Building C",
                Status = "Active",
                Capacity = 15,
                LastInspection = DateTime.Now.AddDays(-60),
                NextInspection = DateTime.Now.AddDays(30),
                Features = new List<string> { "Self-Stressing", "Automated Tensioning", "Temperature Control", "Remote Monitoring" }
            }
        };

        public static List<BedInformation> GetAllBeds()
        {
            return new List<BedInformation>(_beds);
        }

        public static BedInformation? GetBedById(string id)
        {
            return _beds.FirstOrDefault(b => b.Id == id);
        }

        public static List<BedInformation> GetBedsByType(string type)
        {
            return _beds.Where(b => b.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static List<BedInformation> GetActiveBeds()
        {
            return _beds.Where(b => b.Status == "Active").ToList();
        }
    }
}