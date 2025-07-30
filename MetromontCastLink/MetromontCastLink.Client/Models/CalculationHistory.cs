namespace MetromontCastLink.Shared.Models
{
    public class CalculationHistory
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public double Utilization { get; set; }
        public CalculationResult? Result { get; set; }
    }
}