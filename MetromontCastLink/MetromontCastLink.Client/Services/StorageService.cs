using MetromontCastLink.Shared.Models;
using MetromontCastLink.Client.Services;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

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
                    var result = await response.Content.ReadFromJsonAsync<StorageResult>();
                    return result ?? new StorageResult { Success = false, Message = "Failed to parse response" };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new StorageResult
                    {
                        Success = false,
                        Message = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving report: {ex.Message}");
                return new StorageResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        // Implement SaveQCReportAsync to match the interface
        public async Task<StorageResult> SaveQCReportAsync(QCReport report)
        {
            // This delegates to SaveReportAsync for backwards compatibility
            return await SaveReportAsync(report);
        }

        public async Task<List<QCReportListItem>> GetReportsAsync(string projectId)
        {
            try
            {
                var token = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return new List<QCReportListItem>();
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/oss-storage/reports/{projectId}");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    var reports = await response.Content.ReadFromJsonAsync<List<QCReportListItem>>();
                    return reports ?? new List<QCReportListItem>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting reports: {ex.Message}");
            }

            return new List<QCReportListItem>();
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

                var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/oss-storage/report/{bucketKey}/{objectKey}");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<QCReport>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting report: {ex.Message}");
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
                        Message = "Not authenticated with ACC"
                    };
                }

                var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/oss-storage/report/{bucketKey}/{objectKey}");
                httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(httpRequest);
                if (response.IsSuccessStatusCode)
                {
                    return new StorageResult { Success = true, Message = "Report deleted successfully" };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return new StorageResult
                    {
                        Success = false,
                        Message = $"HTTP {response.StatusCode}: {errorContent}"
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting report: {ex.Message}");
                return new StorageResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        private async Task SaveToLocalStorage(QCReport report)
        {
            try
            {
                var json = JsonSerializer.Serialize(report);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"report_{report.ReportId}", json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to local storage: {ex.Message}");
            }
        }
    }
}