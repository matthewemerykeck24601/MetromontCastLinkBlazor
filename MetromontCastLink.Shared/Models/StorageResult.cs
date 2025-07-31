// MetromontCastLink.Shared/Models/StorageResult.cs
using System;

namespace MetromontCastLink.Shared.Models
{
    public class StorageResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? BucketKey { get; set; }
        public string? ObjectKey { get; set; }
        public string? ErrorCode { get; set; }
        public DateTime? Timestamp { get; set; } = DateTime.UtcNow;
    }
}