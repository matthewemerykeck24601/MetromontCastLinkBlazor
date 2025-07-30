using static MetromontCastLink.Client.Components.Pages.Engineering.DesignCalculator;

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
}