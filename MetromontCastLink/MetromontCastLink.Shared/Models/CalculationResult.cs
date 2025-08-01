public class CalculationResult
{
    public string CalculationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string DesignCode { get; set; } = string.Empty;
    public double SafetyFactor { get; set; }
    public double Utilization { get; set; }
    public List<ResultDetail> Details { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}