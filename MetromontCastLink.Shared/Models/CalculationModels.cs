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

        // Additional properties for UI display
        public bool IsValid => Status.Equals("Pass", StringComparison.OrdinalIgnoreCase);
        public bool IsPCICompliant { get; set; } = true;
        public List<string> Messages => Warnings; // Alias for backwards compatibility
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

    public class BeamCalculationData
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double ConcreteStrength { get; set; }
        public double SteelYield { get; set; }
        public double DeadLoad { get; set; }
        public double LiveLoad { get; set; }
        public string BeamType { get; set; } = "";
    }

    public class ColumnCalculationData
    {
        public double Height { get; set; }
        public double Width { get; set; }
        public double Depth { get; set; }
        public double AxialLoad { get; set; }
        public double MomentX { get; set; }
        public double MomentY { get; set; }
        public double ConcreteStrength { get; set; }
        public double SteelYield { get; set; }
    }

    public class DoubleTeeCalculationData
    {
        public double Length { get; set; }
        public double Width { get; set; }
        public double StemDepth { get; set; }
        public double FlangThickness { get; set; }
        public double ConcreteStrength { get; set; }
        public double DeadLoad { get; set; }
        public double LiveLoad { get; set; }
        public int NumberOfStems { get; set; }
    }

    public class ConnectionCalculationData
    {
        public string ConnectionType { get; set; } = "";
        public double ShearForce { get; set; }
        public double TensionForce { get; set; }
        public double MomentForce { get; set; }
        public string MaterialGrade { get; set; } = "";
        public double PlateThickness { get; set; }
    }
}