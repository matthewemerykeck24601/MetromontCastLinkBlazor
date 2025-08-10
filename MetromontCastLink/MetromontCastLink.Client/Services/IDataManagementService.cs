using MetromontCastLink.Shared.Models;
using MetromontCastLink.Shared.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetromontCastLink.Client.Services
{
    public interface IDataManagementService
    {
        Task<List<CloudModel>> GetProjectModelsAsync(string hubId, string projectId);
    }

    public class DataManagementService : IDataManagementService
    {
        private readonly HttpClient _httpClient;
        private readonly IACCService _accService;
        private const string BASE_URL = "https://developer.api.autodesk.com";

        public DataManagementService(HttpClient httpClient, IACCService accService)
        {
            _httpClient = httpClient;
            _accService = accService;
        }

        /// <summary>
        /// Gets all Revit models in a project using the correct API endpoints
        /// </summary>
        public async Task<List<CloudModel>> GetProjectModelsAsync(string hubId, string projectId)
        {
            var models = new List<CloudModel>();

            try
            {
                Console.WriteLine($"Getting models for hub: {hubId}, project: {projectId}");

                // Get the top folders for the project
                var folders = await GetProjectTopFoldersAsync(hubId, projectId);

                if (folders.Count == 0)
                {
                    Console.WriteLine("No folders found. Checking if project exists...");
                    // If no folders, try to get project info to verify access
                    var projectInfo = await GetProjectInfoAsync(hubId, projectId);
                    if (projectInfo == null)
                    {
                        Console.WriteLine("Unable to access project. Check permissions.");
                        return models;
                    }
                }

                Console.WriteLine($"Found {folders.Count} top-level folders to search");

                // Process each folder recursively to find models
                foreach (var folder in folders)
                {
                    Console.WriteLine($"Searching folder: {folder.Name} (ID: {folder.Id})");
                    await SearchFolderForModels(projectId, folder.Id, folder.Name, models);
                }

                Console.WriteLine($"Total models found: {models.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting project models: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            return models;
        }

        /// <summary>
        /// Gets project information to verify access
        /// </summary>
        private async Task<object?> GetProjectInfoAsync(string hubId, string projectId)
        {
            try
            {
                var accessToken = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("No access token available");
                    return null;
                }

                // Use the correct Project API endpoint
                var url = $"{BASE_URL}/project/v1/hubs/{hubId}/projects/{projectId}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Successfully accessed project information");
                    return content;
                }
                else
                {
                    Console.WriteLine($"Failed to get project info: {response.StatusCode}");
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting project info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets the top-level folders for a project
        /// </summary>
        private async Task<List<FolderInfo>> GetProjectTopFoldersAsync(string hubId, string projectId)
        {
            var folders = new List<FolderInfo>();

            try
            {
                var accessToken = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("No access token available");
                    return folders;
                }

                // Get the project's top folders directly
                var url = $"{BASE_URL}/project/v1/hubs/{hubId}/projects/{projectId}/topFolders";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var folder in data.EnumerateArray())
                        {
                            var folderInfo = ParseFolder(folder);
                            if (!string.IsNullOrEmpty(folderInfo.Id))
                            {
                                folders.Add(folderInfo);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to get top folders: {response.StatusCode}");
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error details: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting top folders: {ex.Message}");
            }

            return folders;
        }

        /// <summary>
        /// Recursively searches a folder for Revit models
        /// </summary>
        private async Task SearchFolderForModels(string projectId, string folderId, string folderPath, List<CloudModel> models)
        {
            try
            {
                var contents = await GetFolderContentsAsync(projectId, folderId);

                // Process items (files) in this folder
                foreach (var item in contents.Items)
                {
                    if (IsRevitModel(item))
                    {
                        Console.WriteLine($"Found Revit model: {item.Name}");

                        // Get the latest version info
                        var version = await GetLatestVersionAsync(projectId, item.Id);
                        if (version != null && !string.IsNullOrEmpty(version.Urn))
                        {
                            var model = new CloudModel
                            {
                                Urn = version.Urn,
                                Name = item.Name,
                                LastModified = version.LastModified,
                                Version = $"v{version.VersionNumber}",
                                Type = "Revit Model",
                                MimeType = "application/vnd.autodesk.revit"
                            };

                            models.Add(model);
                            Console.WriteLine($"Added model: {model.Name} (URN: {model.Urn})");
                        }
                    }
                }

                // Recursively search subfolders
                foreach (var subfolder in contents.Folders)
                {
                    var subfolderPath = $"{folderPath}/{subfolder.Name}";
                    Console.WriteLine($"Searching subfolder: {subfolderPath}");
                    await SearchFolderForModels(projectId, subfolder.Id, subfolderPath, models);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching folder {folderPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the contents of a specific folder
        /// </summary>
        private async Task<FolderContents> GetFolderContentsAsync(string projectId, string folderId)
        {
            var contents = new FolderContents
            {
                Folders = new List<FolderInfo>(),
                Items = new List<ItemInfo>()
            };

            try
            {
                var accessToken = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return contents;
                }

                // Use the correct Data Management API v1 endpoint for folder contents
                var url = $"{BASE_URL}/data/v1/projects/{projectId}/folders/{folderId}/contents";

                // Remove restrictive filtering - let's see all files first
                // url += "?filter[extension.type]=items:autodesk.bim360:File,items:autodesk.bim360:C4RModel";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    Console.WriteLine($"=== FOLDER CONTENTS for {folderId}: {json} ===");


                    if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in data.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var type))
                            {
                                var typeStr = type.GetString();

                                if (typeStr == "folders")
                                {
                                    var folderInfo = ParseFolder(item);
                                    if (!string.IsNullOrEmpty(folderInfo.Id))
                                    {
                                        contents.Folders.Add(folderInfo);
                                    }
                                }
                                else if (typeStr == "items")
                                {
                                    var itemInfo = ParseItem(item);
                                    if (!string.IsNullOrEmpty(itemInfo.Id))
                                    {
                                        contents.Items.Add(itemInfo);
                                    }
                                }
                            }
                        }
                    }

                    // Handle pagination if needed
                    if (json.TryGetProperty("links", out var links) &&
                        links.TryGetProperty("next", out var next) &&
                        next.TryGetProperty("href", out var nextUrl))
                    {
                        // TODO: Implement pagination if needed for large folders
                        Console.WriteLine("Additional pages available but not fetched");
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to get folder contents: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting folder contents: {ex.Message}");
            }

            return contents;
        }

        /// <summary>
        /// Gets the latest version of an item
        /// </summary>
        private async Task<VersionInfo?> GetLatestVersionAsync(string projectId, string itemId)
        {
            try
            {
                var accessToken = await _accService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return null;
                }

                var url = $"{BASE_URL}/data/v1/projects/{projectId}/items/{itemId}/versions";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        // Get the first version (latest)
                        var enumerator = data.EnumerateArray();
                        if (enumerator.MoveNext())
                        {
                            var version = enumerator.Current;
                            return ParseVersion(version);
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"Failed to get versions: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting versions: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Extracts hub ID from a project ID
        /// </summary>
        private string ExtractHubId(string projectId)
        {
            // ACC/BIM360 project IDs are in format: b.{hubId}.{projectGuid}
            // or sometimes just b.{guid}
            if (projectId.StartsWith("b."))
            {
                var parts = projectId.Split('.');
                if (parts.Length >= 2)
                {
                    // For ACC, the hub ID is usually the same as the part after "b."
                    // But we need to ensure we use the format b.{hubId}
                    return projectId; // Return the full project ID as hub ID for ACC
                }
            }

            return projectId;
        }

        private FolderInfo ParseFolder(JsonElement folder)
        {
            var info = new FolderInfo();

            if (folder.TryGetProperty("id", out var id))
                info.Id = id.GetString() ?? "";

            if (folder.TryGetProperty("attributes", out var attrs))
            {
                if (attrs.TryGetProperty("name", out var name))
                    info.Name = name.GetString() ?? "";
                if (attrs.TryGetProperty("displayName", out var displayName))
                    info.Name = displayName.GetString() ?? info.Name;
            }

            return info;
        }

        private ItemInfo ParseItem(JsonElement item)
        {
            var info = new ItemInfo();

            if (item.TryGetProperty("id", out var id))
                info.Id = id.GetString() ?? "";

            if (item.TryGetProperty("attributes", out var attrs))
            {
                if (attrs.TryGetProperty("name", out var name))
                    info.Name = name.GetString() ?? "";
                if (attrs.TryGetProperty("displayName", out var displayName))
                    info.Name = displayName.GetString() ?? info.Name;
                if (attrs.TryGetProperty("fileType", out var fileType))
                    info.FileType = fileType.GetString() ?? "";
                if (attrs.TryGetProperty("extension", out var ext))
                {
                    if (ext.TryGetProperty("type", out var extType))
                        info.ExtensionType = extType.GetString() ?? "";
                }
            }

            return info;
        }

        private VersionInfo ParseVersion(JsonElement version)
        {
            var info = new VersionInfo();

            if (version.TryGetProperty("id", out var id))
            {
                var idStr = id.GetString() ?? "";
                // The version ID is already a URN, just remove the "urn:" prefix if present
                info.Urn = idStr.StartsWith("urn:") ? idStr.Substring(4) : idStr;
            }

            if (version.TryGetProperty("attributes", out var attrs))
            {
                if (attrs.TryGetProperty("versionNumber", out var versionNum))
                    info.VersionNumber = versionNum.GetInt32();
                if (attrs.TryGetProperty("lastModifiedTime", out var lastMod))
                    info.LastModified = lastMod.GetDateTime();
                if (attrs.TryGetProperty("storageSize", out var size))
                    info.Size = size.GetInt64();
            }

            // Check if the version has been translated (viewable)
            if (version.TryGetProperty("relationships", out var relationships))
            {
                if (relationships.TryGetProperty("derivatives", out var derivatives))
                {
                    if (derivatives.TryGetProperty("data", out var derivData))
                    {
                        if (derivData.TryGetProperty("id", out var derivId))
                        {
                            // If derivatives exist, the model is viewable
                            info.Status = "viewable";
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(info.Status))
            {
                info.Status = "processing";
            }

            return info;
        }

        private bool IsRevitModel(ItemInfo item)
        {
            // Check if it's a Revit file by extension type or name
            var isRevit = item.ExtensionType?.Contains("C4RModel") == true ||
                         item.ExtensionType?.Contains("RevitFamily") == true ||
                         item.FileType?.ToLower() == "rvt" ||
                         item.Name?.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) == true ||
                         item.Name?.EndsWith(".rfa", StringComparison.OrdinalIgnoreCase) == true;

            return isRevit;
        }

        // Helper classes for Data Management API responses
        private class FolderInfo
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
        }

        private class ItemInfo
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string FileType { get; set; } = "";
            public string ExtensionType { get; set; } = "";
        }

        private class VersionInfo
        {
            public string Urn { get; set; } = "";
            public int VersionNumber { get; set; }
            public DateTime LastModified { get; set; }
            public long Size { get; set; }
            public string Status { get; set; } = "";
        }

        private class FolderContents
        {
            public List<FolderInfo> Folders { get; set; } = new();
            public List<ItemInfo> Items { get; set; } = new();
        }
    }

    // CloudModel class - ensure it exists with proper properties
    public class CloudModel
    {
        public string Urn { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime LastModified { get; set; }
        public string Version { get; set; } = "";
        public string Type { get; set; } = "";
        public string MimeType { get; set; } = "";
    }
}