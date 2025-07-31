// MetromontCastLink.Client/Services/IStorageService.cs
using MetromontCastLink.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MetromontCastLink.Client.Services
{
    public interface IStorageService
    {
        Task<StorageResult> SaveReportAsync(QCReport report);
        Task<List<QCReportListItem>> GetReportsAsync(string projectId);
        Task<QCReport?> GetReportAsync(string bucketKey, string objectKey);
        Task<StorageResult> DeleteReportAsync(string bucketKey, string objectKey);
    }
}