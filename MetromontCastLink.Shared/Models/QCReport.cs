// MetromontCastLink.Shared/Models/QCReport.cs
using System;

namespace MetromontCastLink.Shared.Models
{
    public class QCReport
    {
        public string ReportId { get; set; } = "";
        public string BedId { get; set; } = "";
        public string BedName { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string? ProjectNumber { get; set; }
        public DateTime ReportDate { get; set; }
        public string CalculatedBy { get; set; } = "";
        public string? ReviewedBy { get; set; }
        public string? Location { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public string Status { get; set; } = "Draft";
        public string? OssObjectKey { get; set; }
        public string? OssBucketKey { get; set; }

        public SelfStressingData? SelfStressing { get; set; }
        public NonSelfStressingData? NonSelfStressing { get; set; }
    }

    public class SelfStressingData
    {
        public SelfStressingInputs Inputs { get; set; } = new();
        public SelfStressingResults Results { get; set; } = new();
    }

    public class SelfStressingInputs
    {
        public double InitialPull { get; set; }
        public double RequiredForce { get; set; }
        public double MOE { get; set; }
        public int NumberOfStrands { get; set; }
        public double AdjBedShortening { get; set; }
        public double BlockLength { get; set; }
        public string StrandSize { get; set; } = "";
        public double StrandArea { get; set; }
        public double DeadEndSeating { get; set; }
        public double LiveEndSeating { get; set; }
    }

    public class SelfStressingResults
    {
        public double BasicElongation { get; set; }
        public double BedShortening { get; set; }
        public double TotalElongation { get; set; }
        public double SeatingLoss { get; set; }
        public double DesiredElongationRounded { get; set; }
        public double CalculatedPullRounded { get; set; }
    }

    public class NonSelfStressingData
    {
        public NonSelfStressingInputs Inputs { get; set; } = new();
        public NonSelfStressingResults Results { get; set; } = new();
    }

    public class NonSelfStressingInputs
    {
        public double InitialPull { get; set; }
        public double MOE { get; set; }
        public double BlockLength { get; set; }
        public string StrandSize { get; set; } = "";
        public double StrandArea { get; set; }
        public double TempAtStressing { get; set; }
        public double TempAtRelease { get; set; }
        public double DeadEndSeating { get; set; }
        public double LiveEndSeating { get; set; }
    }

    public class NonSelfStressingResults
    {
        public double BasicElongation { get; set; }
        public double SeatingLoss { get; set; }
        public double TempElongation { get; set; }
        public double TempCorrection { get; set; }
        public double DesiredElongationRounded { get; set; }
        public double CalculatedPullRounded { get; set; }
    }

    public class QCReportListItem
    {
        public string ReportId { get; set; } = "";
        public string BedName { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string CalculatedBy { get; set; } = "";
        public string Status { get; set; } = "";
        public double? SelfStressPull { get; set; }
        public double? NonSelfStressPull { get; set; }
        public DateTime LastModified { get; set; }
        public string Source { get; set; } = ""; // "local" or "oss"
        public string? OssObjectKey { get; set; }
    }
}