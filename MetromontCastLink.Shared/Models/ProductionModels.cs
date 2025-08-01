using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    // Removed ScheduleWorkHours class to avoid conflict with Syncfusion.Blazor.Schedule.ScheduleWorkHours
    // Use Syncfusion's built-in ScheduleWorkHours in views that need it

    public class ProductionEvent
    {
        public string Id { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string ProductType { get; set; } = "";
        public int Quantity { get; set; }
        public string BedId { get; set; } = "";
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; } = "Scheduled";
        public string? DesignNumber { get; set; }
        public string? Notes { get; set; }
    }

    public class BedResource
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string Color { get; set; } = "";
        public bool IsAvailable { get; set; }
    }

    public class BedType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ProductType
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    // Additional production-related models
    public class ProductionSchedule
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ProductionEvent> Events { get; set; } = new();
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class ProductionMetrics
    {
        public int TotalProduction { get; set; }
        public int CompletedToday { get; set; }
        public int InProgress { get; set; }
        public int Scheduled { get; set; }
        public double EfficiencyRate { get; set; }
        public double QualityRate { get; set; }
    }

    public class BedUtilization
    {
        public string BedId { get; set; } = "";
        public string BedName { get; set; } = "";
        public double UtilizationPercentage { get; set; }
        public int TotalHours { get; set; }
        public int UsedHours { get; set; }
        public List<ProductionEvent> CurrentProduction { get; set; } = new();
    }
}