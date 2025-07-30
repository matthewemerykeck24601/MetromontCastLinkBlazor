// MetromontCastLink/MetromontCastLink/Controllers/OssStorageController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MetromontCastLink.Services;
using MetromontCastLink.Shared.Models;

namespace MetromontCastLink.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OssStorageController : ControllerBase
    {
        private readonly IOssStorageService _ossService;
        private readonly IForgeAuthService _forgeAuth;
        private readonly ILogger<OssStorageController> _logger;
        private readonly string _clientId;
        private readonly string _clientSecret;

        public OssStorageController(
            IOssStorageService ossService,
            IForgeAuthService forgeAuth,
            ILogger<OssStorageController> logger,
            IConfiguration configuration)
        {
            _ossService = ossService;
            _forgeAuth = forgeAuth;
            _logger = logger;
            _clientId = configuration["ACC:ClientId"] ?? throw new InvalidOperationException("ACC ClientId not configured");
            _clientSecret = configuration["ACC:ClientSecret"] ?? throw new InvalidOperationException("ACC ClientSecret not configured");
        }

        [HttpPost]
        public async Task<IActionResult> HandleOssRequest([FromBody] OssRequest request)
        {
            try
            {
                _logger.LogInformation($"OSS Storage request: {request.Action}");

                // Get 2-legged token for OSS operations
                var ossToken = await _forgeAuth.Get2LeggedTokenAsync();
                if (string.IsNullOrEmpty(ossToken))
                {
                    return BadRequest(new { success = false, error = "Failed to obtain OSS access token" });
                }

                switch (request.Action?.ToLower())
                {
                    case "save-report":
                        return await SaveReport(ossToken, request.Data);

                    case "load-reports":
                        return await LoadReports(ossToken, request.Data);

                    case "load-report":
                        return await LoadSingleReport(ossToken, request.Data);

                    case "delete-report":
                        return await DeleteReport(ossToken, request.Data);

                    default:
                        return BadRequest(new { success = false, error = $"Unknown action: {request.Action}" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OSS request");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task<IActionResult> SaveReport(string ossToken, JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("projectId", out var projectIdElement) ||
                    !data.TryGetProperty("reportContent", out var reportContentElement))
                {
                    return BadRequest(new { success = false, error = "Missing required data" });
                }

                var projectId = projectIdElement.GetString();
                var reportContent = JsonSerializer.Deserialize<QCReport>(reportContentElement.GetRawText());

                if (string.IsNullOrEmpty(projectId) || reportContent == null)
                {
                    return BadRequest(new { success = false, error = "Invalid data" });
                }

                // Generate bucket key
                var bucketKey = GenerateBucketKey(projectId);

                // Ensure bucket exists
                var bucketResult = await _ossService.EnsureBucketExistsAsync(ossToken, bucketKey);
                if (!bucketResult.Success)
                {
                    return BadRequest(new { success = false, error = bucketResult.Message });
                }

                // Generate object key
                var reportId = reportContent.ReportId;
                var date = DateTime.Now.ToString("yyyyMMdd");
                var objectKey = $"report_{reportId}_{date}.json";

                // Add OSS metadata to report
                reportContent.OssBucketKey = bucketKey;
                reportContent.OssObjectKey = objectKey;

                var reportJson = JsonSerializer.Serialize(reportContent, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Save to OSS using signed S3 upload
                var saveResult = await _ossService.SaveObjectAsync(ossToken, bucketKey, objectKey, reportJson);

                if (saveResult.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        bucketKey = bucketKey,
                        objectKey = objectKey,
                        size = Encoding.UTF8.GetByteCount(reportJson),
                        reportId = reportId,
                        method = "signed-s3-upload",
                        bucketPermissions = "create,read,update,delete",
                        endpoint = "OSS v2 with signed S3 URLs"
                    });
                }
                else
                {
                    return StatusCode(500, new { success = false, error = saveResult.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving report to OSS");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task<IActionResult> LoadReports(string ossToken, JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("projectId", out var projectIdElement))
                {
                    return BadRequest(new { success = false, error = "Missing projectId" });
                }

                var projectId = projectIdElement.GetString();
                if (string.IsNullOrEmpty(projectId))
                {
                    return BadRequest(new { success = false, error = "Invalid projectId" });
                }

                var bucketKey = GenerateBucketKey(projectId);

                // List objects in bucket
                var listResult = await _ossService.ListObjectsAsync(ossToken, bucketKey);

                if (!listResult.Success)
                {
                    // If bucket doesn't exist, return empty list
                    if (listResult.Message?.Contains("404") == true)
                    {
                        return Ok(new
                        {
                            success = true,
                            reports = new List<object>(),
                            message = "No reports found - bucket does not exist yet"
                        });
                    }

                    return StatusCode(500, new { success = false, error = listResult.Message });
                }

                var reports = new List<object>();
                foreach (var item in listResult.Objects)
                {
                    if (item.ObjectKey.EndsWith(".json"))
                    {
                        reports.Add(new
                        {
                            bucketKey = bucketKey,
                            objectKey = item.ObjectKey,
                            displayName = item.ObjectKey.Replace(".json", ""),
                            size = item.Size,
                            lastModified = item.LastModified,
                            source = "oss",
                            needsDownload = true,
                            bucketPermissions = "create,read,update,delete"
                        });
                    }
                }

                return Ok(new
                {
                    success = true,
                    reports = reports,
                    bucketKey = bucketKey,
                    bucketPermissions = "create,read,update,delete"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports from OSS");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task<IActionResult> LoadSingleReport(string ossToken, JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("bucketKey", out var bucketKeyElement) ||
                    !data.TryGetProperty("objectKey", out var objectKeyElement))
                {
                    return BadRequest(new { success = false, error = "Missing required parameters" });
                }

                var bucketKey = bucketKeyElement.GetString();
                var objectKey = objectKeyElement.GetString();

                if (string.IsNullOrEmpty(bucketKey) || string.IsNullOrEmpty(objectKey))
                {
                    return BadRequest(new { success = false, error = "Invalid parameters" });
                }

                var result = await _ossService.GetObjectAsync(ossToken, bucketKey, objectKey);

                if (result.Success && !string.IsNullOrEmpty(result.Content))
                {
                    var report = JsonSerializer.Deserialize<QCReport>(result.Content);
                    return Ok(new
                    {
                        success = true,
                        reportContent = report
                    });
                }
                else
                {
                    return StatusCode(500, new { success = false, error = result.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading single report from OSS");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private async Task<IActionResult> DeleteReport(string ossToken, JsonElement data)
        {
            try
            {
                if (!data.TryGetProperty("bucketKey", out var bucketKeyElement) ||
                    !data.TryGetProperty("objectKey", out var objectKeyElement))
                {
                    return BadRequest(new { success = false, error = "Missing required parameters" });
                }

                var bucketKey = bucketKeyElement.GetString();
                var objectKey = objectKeyElement.GetString();

                if (string.IsNullOrEmpty(bucketKey) || string.IsNullOrEmpty(objectKey))
                {
                    return BadRequest(new { success = false, error = "Invalid parameters" });
                }

                var result = await _ossService.DeleteObjectAsync(ossToken, bucketKey, objectKey);

                if (result.Success)
                {
                    return Ok(new { success = true, message = "Report deleted successfully" });
                }
                else
                {
                    return StatusCode(500, new { success = false, error = result.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting report from OSS");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        private string GenerateBucketKey(string projectId)
        {
            // Clean project ID and generate unique bucket key
            var projectClean = projectId.Replace("-", "").Replace("_", "").ToLower();
            if (projectClean.Length > 10)
            {
                projectClean = projectClean.Substring(0, 10);
            }

            var timestamp = DateTime.Now.Ticks.ToString();
            var timestampSuffix = timestamp.Substring(timestamp.Length - 6);

            return $"metromont-{projectClean}-{timestampSuffix}".ToLower();
        }
    }

    // Request/Response DTOs
    public class OssRequest
    {
        public string? Action { get; set; }
        public JsonElement Data { get; set; }
    }
}