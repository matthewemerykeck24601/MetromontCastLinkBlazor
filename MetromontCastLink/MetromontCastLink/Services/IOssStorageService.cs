// MetromontCastLink/MetromontCastLink/Services/IOssStorageService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MetromontCastLink.Services
{
    public interface IOssStorageService
    {
        Task<OssResult> EnsureBucketExistsAsync(string token, string bucketKey);
        Task<OssResult> SaveObjectAsync(string token, string bucketKey, string objectKey, string content);
        Task<OssListResult> ListObjectsAsync(string token, string bucketKey);
        Task<OssObjectResult> GetObjectAsync(string token, string bucketKey, string objectKey);
        Task<OssResult> DeleteObjectAsync(string token, string bucketKey, string objectKey);
    }

    public class OssResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class OssListResult : OssResult
    {
        public List<OssObject> Objects { get; set; } = new();
    }

    public class OssObjectResult : OssResult
    {
        public string? Content { get; set; }
    }

    public class OssObject
    {
        public string ObjectKey { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
    }