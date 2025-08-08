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
        Task<List<CloudModel>> GetProjectModelsAsync(string projectId, string hubId);
    }

    // Implementation - FULL REPLACEMENT OF EXISTING SKELETON
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
        /// Gets all Revit models in a project
        /// </summary>
        public async Task<List<CloudModel>> GetProjectModelsAsync(string projectId, string hubId)
        {
            var models = new List<CloudModel>();

            try
            {
                Console.WriteLine($"Getting models for project: {projectId}");

                // Get the project's root folder and then traverse for models
                var folders = await GetProjectFoldersAsync(hubId, projectId);

                Console.WriteLine($"Found {folders.Count} folders to search");

                // Process each folder to find models
                foreach (var folder in folders)
                {
                    Console.WriteLine($"Checking folder: {folder.Name}");
                    await GetModelsFromFolder(projectId, folder, models);
                }

                Console.WriteLine($"Total models found: {models.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting project models: {ex.Message}");
            }

            return models;
        }

        /// <summary>
        /// Gets all folders in a project
        /// </summary>
        private async Task<List<FolderInfo>> GetProjectFoldersAsync(string hubId, string projectId)
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

                // For ACC/BIM360, use the Data Management API to get the root folder first
                // Then get its contents
                var url = $"{BASE_URL}/data/v1/projects/{projectId}/project";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    // Get the root folder ID
                    if (json.TryGetProperty("data", out var data))
                    {
                        if (data.TryGetProperty("relationships", out var relationships))
                        {
                            if (relationships.TryGetProperty("rootFolder", out var rootFolder))
                            {
                                if (rootFolder.TryGetProperty("data", out var rootFolderData))
                                {
                                    if (rootFolderData.TryGetProperty("id", out var rootFolderId))
                                    {
                                        var rootId = rootFolderId.GetString();
                                        Console.WriteLine($"Found root folder: {rootId}");

                                        // Now get the contents of the root folder
                                        var contents = await GetFolderContentsAsync(projectId, rootId ?? "");
                                        folders.AddRange(contents.Folders);

                                        // Return the root folder itself as well so we can check for models in it
                                        var rootFolderInfo = new FolderInfo { Id = rootId ?? "", Name = "Project Files" };
                                        folders.Insert(0, rootFolderInfo);
                                    }
                                }
                            }
                        }
                    }
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
                Console.WriteLine($"Error getting project folders: {ex.Message}");
            }

            return folders;
        }

        /// <summary>
        /// Gets contents of a specific folder
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
                    Console.WriteLine("No access token available");
                    return contents;
                }

                var url = $"{BASE_URL}/data/v1/projects/{projectId}/folders/{folderId}/contents";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    if (json.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in data.EnumerateArray())
                        {
                            if (item.TryGetProperty("type", out var type))
                            {
                                var typeStr = type.GetString();
                                if (typeStr == "folders")
                                {
                                    contents.Folders.Add(ParseFolder(item));
                                }
                                else if (typeStr == "items")
                                {
                                    var itemInfo = ParseItem(item);
                                    // Only include Revit models
                                    if (IsRevitModel(itemInfo))
                                    {
                                        contents.Items.Add(itemInfo);
                                    }
                                }
                            }
                        }
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
        /// Recursively gets models from a folder and its subfolders
        /// </summary>
        private async Task GetModelsFromFolder(string projectId, FolderInfo folder, List<CloudModel> models)
        {
            try
            {
                var contents = await GetFolderContentsAsync(projectId, folder.Id);

                // Add models from this folder
                foreach (var item in contents.Items)
                {
                    // Get the latest version of the item
                    var version = await GetLatestVersionAsync(projectId, item.Id);
                    if (version != null)
                    {
                        models.Add(new CloudModel
                        {
                            Urn = version.Urn,
                            Name = item.Name,
                            LastModified = version.LastModified,
                            Version = $"v{version.VersionNumber}",
                            Type = "Revit",
                            MimeType = "application/vnd.autodesk.revit"
                        });

                        Console.WriteLine($"Found model: {item.Name} (URN: {version.Urn})");
                    }
                }

                // Recursively process subfolders
                foreach (var subfolder in contents.Folders)
                {
                    await GetModelsFromFolder(projectId, subfolder, models);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing folder {folder.Name}: {ex.Message}");
            }
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
                // The ID is in format urn:adsk.wipxxx:fs.file:vf.xxx?version=n
                // We need to extract just the URN part (remove the urn: prefix)
                if (idStr.StartsWith("urn:"))
                {
                    info.Urn = idStr.Substring(4); // Remove "urn:" prefix
                }
                else
                {
                    info.Urn = idStr;
                }
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
            // Check if it's a Revit file
            return item.FileType?.ToLower() == "rvt" ||
                   item.Name?.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) == true ||
                   item.ExtensionType?.Contains("C4RModel") == true;
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

    // CloudModel class already exists in the file, but let's make sure it has all needed properties
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