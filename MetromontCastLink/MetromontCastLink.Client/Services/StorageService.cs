// MetromontCastLink.Client/Services/StorageService.cs
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using MetromontCastLink.Shared.Models;
using Microsoft.JSInterop;

namespace MetromontCastLink.Client.Services
{
    public class StorageService : IStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly IACCService _accService;
        private readonly IJSRuntime _jsRuntime;

        public StorageService(HttpClient httpClient, IACCService accService, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _accService = accService;
            _jsRuntime = jsRuntime;
        }

        public async Task<StorageResult> SaveReportAsync(QCReport report)
        {
            try
            {
                // First, save to local storage as backup
                await SaveToLocalStorage(report);

                // Get access token
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return new StorageResult
                    {
                        Success = false,
                        Message = "Not authenticated with ACC"
                    };
                }

                // Prepare the request
                var request = new
                {
                    action = "save-report",
                    data = new
                    {
                        projectId = report.ProjectId,
                        reportContent = report
                    }
                };

                // Call the OSS storage API
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OssStorageResponse>();
                    if (result != null && result.Success)
                    {
                        // Update report with OSS keys
                        report.OssBucketKey = result.BucketKey;
                        report.OssObjectKey = result.ObjectKey;
                        await SaveToLocalStorage(report); // Update local copy

                        return new StorageResult
                        {
                            Success = true,
                            Message = "Report saved successfully",
                            BucketKey = result.BucketKey,
                            ObjectKey = result.ObjectKey
                        };
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                return new StorageResult
                {
                    Success = false,
                    Message = $"Failed to save to OSS: {errorContent}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving report: {ex.Message}");
                return new StorageResult
                {
                    Success = false,
                    Message = $"Error saving report: {ex.Message}"
                };
            }
        }

        public async Task<List<QCReportListItem>> GetReportsAsync(string projectId)
        {
            var reports = new List<QCReportListItem>();

            try
            {
                // Get local reports
                var localReports = await GetLocalReports(projectId);
                reports.AddRange(localReports);

                // Get OSS reports
                var token = await _accService.GetAccessTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    request.Content = JsonContent.Create(new
                    {
                        action = "load-reports",
                        data = new { projectId = projectId }
                    });

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadFromJsonAsync<OssReportsResponse>();
                        if (result != null && result.Success && result.Reports != null)
                        {
                            foreach (var ossReport in result.Reports)
                            {
                                // Check if we already have this report locally
                                if (!reports.Any(r => r.ReportId == ossReport.DisplayName))
                                {
                                    reports.Add(new QCReportListItem
                                    {
                                        ReportId = ossReport.DisplayName,
                                        BedName = ExtractBedName(ossReport.DisplayName),
                                        ProjectName = "From OSS",
                                        ReportDate = ossReport.LastModified,
                                        Status = "Completed",
                                        Source = "oss",
                                        OssObjectKey = ossReport.ObjectKey,
                                        LastModified = ossReport.LastModified
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reports: {ex.Message}");
            }

            return reports.OrderByDescending(r => r.LastModified).ToList();
        }

        public async Task<QCReport?> GetReportAsync(string bucketKey, string objectKey)
        {
            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new
                {
                    action = "load-report",
                    data = new
                    {
                        bucketKey = bucketKey,
                        objectKey = objectKey
                    }
                });

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OssReportResponse>();
                    if (result != null && result.Success && result.ReportContent != null)
                    {
                        return result.ReportContent;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading report: {ex.Message}");
            }

            return null;
        }

        public async Task<StorageResult> DeleteReportAsync(string bucketKey, string objectKey)
        {
            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return new StorageResult
                    {
                        Success = false,
                        Message = "Not authenticated"
                    };
                }

                var request = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Content = JsonContent.Create(new
                {
                    action = "delete-report",
                    data = new
                    {
                        bucketKey = bucketKey,
                        objectKey = objectKey
                    }
                });

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return new StorageResult
                    {
                        Success = true,
                        Message = "Report deleted successfully"
                    };
                }

                return new StorageResult
                {
                    Success = false,
                    Message = "Failed to delete report"
                };
            }
            catch (Exception ex)
            {
                return new StorageResult
                {
                    Success = false,
                    Message = $"Error deleting report: {ex.Message}"
                };
            }
        }

        private async Task SaveToLocalStorage(QCReport report)
        {
            try
            {
                var key = $"qc_report_{report.ReportId}";
                var json = JsonSerializer.Serialize(report);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, json);

                // Also update the reports index
                var indexKey = $"qc_reports_index_{report.ProjectId}";
                var indexJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", indexKey);
                var index = string.IsNullOrEmpty(indexJson)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(indexJson) ?? new List<string>();

                if (!index.Contains(report.ReportId))
                {
                    index.Add(report.ReportId);
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", indexKey, JsonSerializer.Serialize(index));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to local storage: {ex.Message}");
            }
        }

        private async Task<List<QCReportListItem>> GetLocalReports(string projectId)
        {
            var reports = new List<QCReportListItem>();

            try
            {
                var indexKey = $"qc_reports_index_{projectId}";
                var indexJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", indexKey);

                if (!string.IsNullOrEmpty(indexJson))
                {
                    var index = JsonSerializer.Deserialize<List<string>>(indexJson);
                    if (index != null)
                    {
                        foreach (var reportId in index)
                        {
                            var reportKey = $"qc_report_{reportId}";
                            var reportJson = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", reportKey);

                            if (!string.IsNullOrEmpty(reportJson))
                            {
                                var report = JsonSerializer.Deserialize<QCReport>(reportJson);
                                if (report != null)
                                {
                                    reports.Add(new QCReportListItem
                                    {
                                        ReportId = report.ReportId,
                                        BedName = report.BedName,
                                        ProjectName = report.ProjectName,
                                        ReportDate = report.ReportDate,
                                        CalculatedBy = report.CalculatedBy,
                                        Status = report.Status,
                                        SelfStressPull = report.SelfStressing?.Results?.CalculatedPullRounded,
                                        NonSelfStressPull = report.NonSelfStressing?.Results?.CalculatedPullRounded,
                                        LastModified = report.ModifiedDate ?? report.CreatedDate,
                                        Source = "local",
                                        OssObjectKey = report.OssObjectKey
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading local reports: {ex.Message}");
            }

            return reports;
        }

        private string ExtractBedName(string displayName)
        {
            // Extract bed name from display name format: "report_BR-20240115-ABCD1234_20240115.json"
            var parts = displayName.Split('_');
            if (parts.Length >= 2)
            {
                var reportIdParts = parts[1].Split('-');
                if (reportIdParts.Length >= 3)
                {
                    return "Unknown Bed"; // Would need to be parsed from the actual report content
                }
            }
            return displayName;
        }

        // Response DTOs
        private class OssStorageResponse
        {
            public bool Success { get; set; }
            public string? BucketKey { get; set; }
            public string? ObjectKey { get; set; }
            public string? Error { get; set; }
        }

        private class OssReportsResponse
        {
            public bool Success { get; set; }
            public List<OssReportItem>? Reports { get; set; }
        }

        private class OssReportItem
        {
            public string BucketKey { get; set; } = "";
            public string ObjectKey { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public long Size { get; set; }
            public DateTime LastModified { get; set; }
        }

        private class OssReportResponse
        {
            public bool Success { get; set; }
            public QCReport? ReportContent { get; set; }
        }
    }
}