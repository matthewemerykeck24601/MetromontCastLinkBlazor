// Models/BedInformation.cs
namespace MetromontCastLink.Shared.Models
{
    public class BedInformation
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public double Length { get; set; }
        public double Width { get; set; }
        public double Capacity { get; set; }
        public List<string> AllowedProductTypes { get; set; } = new();
        public string Location { get; set; } = "";
        public string Status { get; set; } = "Active";
        public BedSpecifications Specifications { get; set; } = new();
    }

    public class BedSpecifications
    {
        public double MaxStrandForce { get; set; }
        public double MaxProductLength { get; set; }
        public double MaxProductWidth { get; set; }
        public double MaxProductHeight { get; set; }
        public bool SelfStressing { get; set; }
        public bool HeatedBed { get; set; }
        public double? TemperatureRange { get; set; }
    }

    public static class BedDatabase
    {
        public static List<BedInformation> GetAllBeds()
        {
            return new List<BedInformation>
            {
                new BedInformation
                {
                    Id = "beam",
                    Name = "Beam",
                    Type = "Beam Bed",
                    Length = 400,
                    Width = 8,
                    Capacity = 50000,
                    AllowedProductTypes = new List<string> { "Beam", "Girder", "Joist" },
                    Location = "Building A - Section 1",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 50000,
                        MaxProductLength = 100,
                        MaxProductWidth = 8,
                        MaxProductHeight = 6,
                        SelfStressing = true,
                        HeatedBed = true,
                        TemperatureRange = 150
                    }
                },
                new BedInformation
                {
                    Id = "deck1",
                    Name = "Deck 1",
                    Type = "Deck Bed",
                    Length = 300,
                    Width = 12,
                    Capacity = 75000,
                    AllowedProductTypes = new List<string> { "Double Tee", "Single Tee", "Deck Panel" },
                    Location = "Building A - Section 2",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 75000,
                        MaxProductLength = 60,
                        MaxProductWidth = 12,
                        MaxProductHeight = 3,
                        SelfStressing = true,
                        HeatedBed = true,
                        TemperatureRange = 140
                    }
                },
                new BedInformation
                {
                    Id = "deck2",
                    Name = "Deck 2",
                    Type = "Deck Bed",
                    Length = 300,
                    Width = 12,
                    Capacity = 75000,
                    AllowedProductTypes = new List<string> { "Double Tee", "Single Tee", "Deck Panel" },
                    Location = "Building A - Section 3",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 75000,
                        MaxProductLength = 60,
                        MaxProductWidth = 12,
                        MaxProductHeight = 3,
                        SelfStressing = true,
                        HeatedBed = true,
                        TemperatureRange = 140
                    }
                },
                new BedInformation
                {
                    Id = "flatbed1",
                    Name = "Flat Bed #1",
                    Type = "Flat Bed",
                    Length = 200,
                    Width = 20,
                    Capacity = 60000,
                    AllowedProductTypes = new List<string> { "Wall Panel", "Flat Slab", "Solid Slab", "Column" },
                    Location = "Building B - Section 1",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 60000,
                        MaxProductLength = 40,
                        MaxProductWidth = 20,
                        MaxProductHeight = 2,
                        SelfStressing = false,
                        HeatedBed = true,
                        TemperatureRange = 120
                    }
                },
                new BedInformation
                {
                    Id = "flatbed2",
                    Name = "Flat Bed #2",
                    Type = "Flat Bed",
                    Length = 200,
                    Width = 20,
                    Capacity = 60000,
                    AllowedProductTypes = new List<string> { "Wall Panel", "Flat Slab", "Solid Slab", "Column" },
                    Location = "Building B - Section 2",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 60000,
                        MaxProductLength = 40,
                        MaxProductWidth = 20,
                        MaxProductHeight = 2,
                        SelfStressing = false,
                        HeatedBed = true,
                        TemperatureRange = 120
                    }
                },
                new BedInformation
                {
                    Id = "pcbed1",
                    Name = "PC Bed #1",
                    Type = "Column Bed",
                    Length = 150,
                    Width = 10,
                    Capacity = 40000,
                    AllowedProductTypes = new List<string> { "Column", "Pile", "Post" },
                    Location = "Building C - Section 1",
                    Specifications = new BedSpecifications
                    {
                        MaxStrandForce = 40000,
                        MaxProductLength = 50,
                        MaxProductWidth = 3,
                        MaxProductHeight = 3,
                        SelfStressing = false,
                        HeatedBed = false
                    }
                }
            };
        }

        public static BedInformation? GetBedById(string id)
        {
            return GetAllBeds().FirstOrDefault(b => b.Id == id);
        }

        public static List<BedInformation> GetBedsByType(string type)
        {
            return GetAllBeds().Where(b => b.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static List<BedInformation> GetBedsForProductType(string productType)
        {
            return GetAllBeds().Where(b => b.AllowedProductTypes.Contains(productType)).ToList();
        }
    }
}