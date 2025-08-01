using MetromontCastLink.Shared.Models;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetromontCastLink.Client.Services
{
    public class StorageService : IStorageService
    {
        private readonly IACCService _accService;
        private readonly IJSRuntime _jsRuntime;
        private readonly HttpClient _httpClient;

        public StorageService(IACCService accService, IJSRuntime jsRuntime, HttpClient httpClient)
        {
            _accService = accService;
            _jsRuntime = jsRuntime;
            _httpClient = httpClient;
        }

        public async Task<StorageResult> SaveReportAsync(QCReport report)
        {
            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return new StorageResult { Success = false, Message = "Not authenticated" };
                }

                // Save to local storage first as backup
                var json = JsonSerializer.Serialize(report);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"qc_report_{report.ReportId}", json);

                // Create request for OSS storage
                var request = new
                {
                    action = "save-report",
                    data = report
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<StorageResult>();
                    return result ?? new StorageResult { Success = false, Message = "Invalid response" };
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new StorageResult { Success = false, Message = $"Error: {error}" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving report: {ex.Message}");
                return new StorageResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<StorageResult> SaveQCReportAsync(QCReport report)
        {
            // This method just calls SaveReportAsync - they're the same implementation
            return await SaveReportAsync(report);
        }

        public async Task<List<QCReportListItem>> GetReportsAsync(string projectId)
        {
            var reports = new List<QCReportListItem>();

            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return reports;
                }

                var request = new
                {
                    action = "load-reports",
                    projectId = projectId
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OSSReportsResponse>();
                    return result?.Reports ?? reports;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading reports: {ex.Message}");
            }

            return reports;
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

                var request = new
                {
                    action = "load-report",
                    bucketKey = bucketKey,
                    objectKey = objectKey
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<OSSReportResponse>();
                    return result?.ReportContent;
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
                    return new StorageResult { Success = false, Message = "Not authenticated" };
                }

                var request = new
                {
                    action = "delete-report",
                    bucketKey = bucketKey,
                    objectKey = objectKey
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<StorageResult>();
                    return result ?? new StorageResult { Success = false, Message = "Invalid response" };
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    return new StorageResult { Success = false, Message = $"Error: {error}" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting report: {ex.Message}");
                return new StorageResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<bool> SaveCalculationAsync(CalculationResult calculation)
        {
            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                // Generate unique ID for the calculation
                var calculationId = Guid.NewGuid().ToString();
                var timestamp = DateTime.UtcNow;

                var calculationData = new
                {
                    id = calculationId,
                    calculation = calculation,
                    timestamp = timestamp,
                    projectId = await GetCurrentProjectId()
                };

                // Save to local storage first
                var json = JsonSerializer.Serialize(calculationData);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"calculation_{calculationId}", json);

                // Save to OSS if connected
                var request = new
                {
                    action = "save-calculation",
                    data = calculationData
                };

                var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/oss-storage");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                httpRequest.Content = JsonContent.Create(request);

                var response = await _httpClient.SendAsync(httpRequest);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving calculation: {ex.Message}");
                return false;
            }
        }

        public async Task<List<CalculationHistory>> GetCalculationHistoryAsync(string projectId)
        {
            var history = new List<CalculationHistory>();

            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return history;
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/oss-storage/calculations/{projectId}");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    var calculations = await response.Content.ReadFromJsonAsync<List<CalculationHistory>>();
                    return calculations ?? history;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting calculation history: {ex.Message}");
            }

            return history;
        }

        public async Task<CalculationResult?> GetCalculationAsync(string calculationId)
        {
            try
            {
                // First try local storage
                var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", $"calculation_{calculationId}");
                if (!string.IsNullOrEmpty(json))
                {
                    var data = JsonSerializer.Deserialize<CalculationData>(json);
                    return data?.Calculation;
                }

                // If not in local storage, try OSS
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/oss-storage/calculation/{calculationId}");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<CalculationResult>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting calculation: {ex.Message}");
            }

            return null;
        }

        private async Task<string?> GetCurrentProjectId()
        {
            var currentProject = await _accService.GetCurrentProjectAsync();
            return currentProject?.Id;
        }

        // Helper classes for JSON deserialization
        private class OSSReportsResponse
        {
            public bool Success { get; set; }
            public List<QCReportListItem> Reports { get; set; } = new();
        }

        private class OSSReportResponse
        {
            public bool Success { get; set; }
            public QCReport? ReportContent { get; set; }
        }

        private class CalculationData
        {
            public string Id { get; set; } = "";
            public CalculationResult? Calculation { get; set; }
            public DateTime Timestamp { get; set; }
            public string? ProjectId { get; set; }
        }
    }
}