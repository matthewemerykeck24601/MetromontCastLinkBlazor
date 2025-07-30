// MetromontCastLink/MetromontCastLink/Services/OssStorageService.cs
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MetromontCastLink.Services
{
    public class OssStorageService : IOssStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OssStorageService> _logger;
        private const string OSS_BASE_URL = "https://developer.api.autodesk.com/oss/v2";

        public OssStorageService(HttpClient httpClient, ILogger<OssStorageService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<OssResult> EnsureBucketExistsAsync(string token, string bucketKey)
        {
            try
            {
                _logger.LogInformation($"Checking if bucket exists: {bucketKey}");

                // Check if bucket exists
                var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{OSS_BASE_URL}/buckets/{bucketKey}/details");
                checkRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var checkResponse = await _httpClient.SendAsync(checkRequest);

                if (checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Bucket already exists: {bucketKey}");
                    return new OssResult { Success = true, Message = "Bucket exists" };
                }

                if (checkResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation($"Creating new bucket: {bucketKey}");

                    // Create bucket
                    var createRequest = new HttpRequestMessage(HttpMethod.Post, $"{OSS_BASE_URL}/buckets");
                    createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var bucketData = new
                    {
                        bucketKey = bucketKey,
                        policyKey = "persistent"
                    };

                    createRequest.Content = new StringContent(
                        JsonSerializer.Serialize(bucketData),
                        Encoding.UTF8,
                        "application/json"
                    );

                    var createResponse = await _httpClient.SendAsync(createRequest);

                    if (createResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation($"Successfully created bucket: {bucketKey}");
                        return new OssResult { Success = true, Message = "Bucket created" };
                    }

                    var errorContent = await createResponse.Content.ReadAsStringAsync();

                    // Check if it's a race condition where bucket was created by another request
                    if (errorContent.Contains("already exists"))
                    {
                        _logger.LogInformation("Bucket already exists (race condition)");
                        return new OssResult { Success = true, Message = "Bucket exists" };
                    }

                    _logger.LogError($"Failed to create bucket: {createResponse.StatusCode} - {errorContent}");
                    return new OssResult { Success = false, Message = $"Failed to create bucket: {errorContent}" };
                }

                var checkError = await checkResponse.Content.ReadAsStringAsync();
                return new OssResult { Success = false, Message = $"Bucket check failed: {checkError}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EnsureBucketExists");
                return new OssResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<OssResult> SaveObjectAsync(string token, string bucketKey, string objectKey, string content)
        {
            try
            {
                _logger.LogInformation($"Saving object using signed S3 upload: {bucketKey}/{objectKey}");

                var fileBytes = Encoding.UTF8.GetBytes(content);
                var fileSize = fileBytes.Length;

                // Step 1: Request signed upload URL
                _logger.LogInformation("Step 1: Requesting signed upload URL");

                var signedUrlRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{OSS_BASE_URL}/buckets/{bucketKey}/objects/{Uri.EscapeDataString(objectKey)}/signeds3upload?parts=1"
                );
                signedUrlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var signedUrlResponse = await _httpClient.SendAsync(signedUrlRequest);

                if (!signedUrlResponse.IsSuccessStatusCode)
                {
                    var errorContent = await signedUrlResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get signed URL: {signedUrlResponse.StatusCode} - {errorContent}");
                    return new OssResult { Success = false, Message = $"Failed to get signed URL: {errorContent}" };
                }

                var signedUrlData = await signedUrlResponse.Content.ReadAsStringAsync();
                var signedUpload = JsonSerializer.Deserialize<SignedUploadResponse>(signedUrlData);

                if (signedUpload?.Urls == null || signedUpload.Urls.Length == 0)
                {
                    return new OssResult { Success = false, Message = "No upload URLs in response" };
                }

                // Step 2: Upload to S3
                _logger.LogInformation($"Step 2: Uploading {fileSize} bytes to S3");

                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, signedUpload.Urls[0]);
                uploadRequest.Content = new ByteArrayContent(fileBytes);
                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var uploadResponse = await _httpClient.SendAsync(uploadRequest);

                if (!uploadResponse.IsSuccessStatusCode)
                {
                    var errorContent = await uploadResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to upload to S3: {uploadResponse.StatusCode} - {errorContent}");
                    return new OssResult { Success = false, Message = $"Failed to upload to S3: {errorContent}" };
                }

                // Step 3: Finalize upload
                _logger.LogInformation("Step 3: Finalizing upload");

                var finalizeRequest = new HttpRequestMessage(HttpMethod.Post, signedUpload.CompleteUploadUrl);
                finalizeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var partsData = new
                {
                    parts = new[]
                    {
                        new
                        {
                            PartNumber = 1,
                            ETag = uploadResponse.Headers.ETag?.Tag ?? ""
                        }
                    }
                };

                finalizeRequest.Content = new StringContent(
                    JsonSerializer.Serialize(partsData),
                    Encoding.UTF8,
                    "application/json"
                );

                var finalizeResponse = await _httpClient.SendAsync(finalizeRequest);

                if (!finalizeResponse.IsSuccessStatusCode)
                {
                    var errorContent = await finalizeResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to finalize upload: {finalizeResponse.StatusCode} - {errorContent}");
                    return new OssResult { Success = false, Message = $"Failed to finalize upload: {errorContent}" };
                }

                _logger.LogInformation($"Successfully uploaded object: {bucketKey}/{objectKey}");
                return new OssResult { Success = true, Message = "Object saved successfully" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SaveObject");
                return new OssResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<OssListResult> ListObjectsAsync(string token, string bucketKey)
        {
            try
            {
                _logger.LogInformation($"Listing objects in bucket: {bucketKey}");

                var request = new HttpRequestMessage(HttpMethod.Get, $"{OSS_BASE_URL}/buckets/{bucketKey}/objects");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation("Bucket does not exist");
                        return new OssListResult
                        {
                            Success = false,
                            Message = "Bucket does not exist (404)",
                            Objects = new List<OssObject>()
                        };
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to list objects: {response.StatusCode} - {errorContent}");
                    return new OssListResult
                    {
                        Success = false,
                        Message = $"Failed to list objects: {errorContent}",
                        Objects = new List<OssObject>()
                    };
                }

                var content = await response.Content.ReadAsStringAsync();
                var listData = JsonSerializer.Deserialize<ObjectListResponse>(content);

                var result = new OssListResult { Success = true };

                if (listData?.Items != null)
                {
                    result.Objects = listData.Items.Select(item => new OssObject
                    {
                        ObjectKey = item.ObjectKey ?? "",
                        Size = item.Size,
                        LastModified = DateTimeOffset.FromUnixTimeMilliseconds(item.LastModified).DateTime
                    }).ToList();
                }

                _logger.LogInformation($"Found {result.Objects.Count} objects in bucket");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ListObjects");
                return new OssListResult
                {
                    Success = false,
                    Message = ex.Message,
                    Objects = new List<OssObject>()
                };
            }
        }

        public async Task<OssObjectResult> GetObjectAsync(string token, string bucketKey, string objectKey)
        {
            try
            {
                _logger.LogInformation($"Getting object: {bucketKey}/{objectKey}");

                // First get signed download URL
                var signedUrlRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"{OSS_BASE_URL}/buckets/{bucketKey}/objects/{Uri.EscapeDataString(objectKey)}/signeds3download"
                );
                signedUrlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var signedUrlResponse = await _httpClient.SendAsync(signedUrlRequest);

                if (!signedUrlResponse.IsSuccessStatusCode)
                {
                    var errorContent = await signedUrlResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to get signed download URL: {signedUrlResponse.StatusCode} - {errorContent}");
                    return new OssObjectResult { Success = false, Message = $"Failed to get download URL: {errorContent}" };
                }

                var signedUrlData = await signedUrlResponse.Content.ReadAsStringAsync();
                var signedDownload = JsonSerializer.Deserialize<SignedDownloadResponse>(signedUrlData);

                if (string.IsNullOrEmpty(signedDownload?.Url))
                {
                    return new OssObjectResult { Success = false, Message = "No download URL in response" };
                }

                // Download from S3
                var downloadRequest = new HttpRequestMessage(HttpMethod.Get, signedDownload.Url);
                var downloadResponse = await _httpClient.SendAsync(downloadRequest);

                if (!downloadResponse.IsSuccessStatusCode)
                {
                    var errorContent = await downloadResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to download from S3: {downloadResponse.StatusCode} - {errorContent}");
                    return new OssObjectResult { Success = false, Message = $"Failed to download: {errorContent}" };
                }

                var content = await downloadResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"Successfully downloaded object: {objectKey}");

                return new OssObjectResult
                {
                    Success = true,
                    Content = content
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetObject");
                return new OssObjectResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<OssResult> DeleteObjectAsync(string token, string bucketKey, string objectKey)
        {
            try
            {
                _logger.LogInformation($"Deleting object: {bucketKey}/{objectKey}");

                var request = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"{OSS_BASE_URL}/buckets/{bucketKey}/objects/{Uri.EscapeDataString(objectKey)}"
                );
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Successfully deleted object: {objectKey}");
                    return new OssResult { Success = true, Message = "Object deleted successfully" };
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to delete object: {response.StatusCode} - {errorContent}");
                return new OssResult { Success = false, Message = $"Failed to delete: {errorContent}" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeleteObject");
                return new OssResult { Success = false, Message = ex.Message };
            }
        }

        // Response DTOs
        private class SignedUploadResponse
        {
            [JsonPropertyName("uploadKey")]
            public string? UploadKey { get; set; }

            [JsonPropertyName("uploadExpiration")]
            public long UploadExpiration { get; set; }

            [JsonPropertyName("urls")]
            public string[]? Urls { get; set; }

            [JsonPropertyName("completeUploadUrl")]
            public string? CompleteUploadUrl { get; set; }
        }

        private class SignedDownloadResponse
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }

            [JsonPropertyName("expiration")]
            public long Expiration { get; set; }
        }

        private class ObjectListResponse
        {
            [JsonPropertyName("items")]
            public ObjectItem[]? Items { get; set; }

            [JsonPropertyName("next")]
            public string? Next { get; set; }
        }

        private class ObjectItem
        {
            [JsonPropertyName("bucketKey")]
            public string? BucketKey { get; set; }

            [JsonPropertyName("objectKey")]
            public string? ObjectKey { get; set; }

            [JsonPropertyName("size")]
            public long Size { get; set; }

            [JsonPropertyName("lastModifiedDate")]
            public long LastModified { get; set; }
        }
    }
}