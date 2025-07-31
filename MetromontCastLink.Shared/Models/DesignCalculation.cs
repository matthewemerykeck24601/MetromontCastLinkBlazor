// MetromontCastLink.Shared/Models/DesignCalculation.cs
using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    public class DesignCalculation
    {
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string CalculationType { get; set; } = "";
        public string ElementType { get; set; } = "";
        public string ElementId { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public string Status { get; set; } = "Draft";
        public Dictionary<string, object> Inputs { get; set; } = new();
        public Dictionary<string, object> Results { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public bool IsValid { get; set; } = true;
        public string Notes { get; set; } = "";
    }

    public class ProjectData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime LastModified { get; set; }
        public string Status { get; set; } = "Active";
        public Dictionary<string, object> Properties { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }
}