using MetromontCastLink.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MetromontCastLink.Client.Services
{
    public interface IStorageService
    {
        Task<StorageResult> SaveFileAsync(string fileName, string content);
        Task<StorageResult> SaveReportAsync(QCReport report);
        Task<StorageResult> SaveQCReportAsync(QCReport report);
        Task<List<QCReportListItem>> GetReportsAsync(string projectId);
        Task<QCReport?> GetReportAsync(string bucketKey, string objectKey);
        Task<StorageResult> DeleteReportAsync(string bucketKey, string objectKey);

        // Add method for saving calculation results
        Task<bool> SaveCalculationAsync(CalculationResult calculation);
        Task<List<CalculationHistory>> GetCalculationHistoryAsync(string projectId);
        Task<CalculationResult?> GetCalculationAsync(string calculationId);
    }
}