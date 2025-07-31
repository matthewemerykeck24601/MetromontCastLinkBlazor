using System;
using System.Collections.Generic;

namespace MetromontCastLink.Shared.Models
{
    public class CalculationResult
    {
        public string CalculationType { get; set; } = "";
        public string Status { get; set; } = "";
        public string DesignCode { get; set; } = "";
        public double SafetyFactor { get; set; }
        public double Utilization { get; set; }
        public List<ResultDetail> Details { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ResultDetail
    {
        public string Label { get; set; } = "";
        public string Value { get; set; } = "";
        public string Unit { get; set; } = "";
    }

    public class CalculationHistory
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double Utilization { get; set; }
        public CalculationResult? Result { get; set; }
    }
}