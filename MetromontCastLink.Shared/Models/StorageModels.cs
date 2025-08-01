// Add these models to MetromontCastLink.Shared/Models/StorageModels.cs (create file if it doesn't exist)

namespace MetromontCastLink.Shared.Models
{
    public class StorageResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? BucketKey { get; set; }
        public string? ObjectKey { get; set; }
    }

    public class QCReportListItem
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string BedId { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string ReportedBy { get; set; } = "";
        public string BucketKey { get; set; } = "";
        public string ObjectKey { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }

    public class CalculationHistory
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string CalculationType { get; set; } = "";
        public DateTime CalculationDate { get; set; }
        public string CalculatedBy { get; set; } = "";
        public string? Description { get; set; }
    }

    public class CalculationResult
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public Dictionary<string, object> Inputs { get; set; } = new();
        public Dictionary<string, object> Results { get; set; } = new();
        public DateTime CalculationDate { get; set; }
        public string? Notes { get; set; }
    }
}