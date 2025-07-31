using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    public class ScheduleWorkHours
    {
        public int DayOfWeek { get; set; }
        public string StartHour { get; set; } = "06:00";
        public string EndHour { get; set; } = "18:00";
    }

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
}