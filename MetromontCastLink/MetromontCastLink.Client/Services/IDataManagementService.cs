using MetromontCastLink.Shared.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MetromontCastLink.Client.Services
{
    public interface IDataManagementService
    {
        Task<List<CloudModel>> GetProjectModelsAsync(string projectId, string hubId);
    }

    // Implementation
    public class DataManagementService : IDataManagementService
    {
        public async Task<List<CloudModel>> GetProjectModelsAsync(string projectId, string hubId)
        {
            // TODO: Implement actual Data Management API calls
            // 1. Get access token
            // 2. Call Autodesk API to get folders
            // 3. Get folder contents
            // 4. Filter for Revit files
            // 5. Return list of CloudModel objects

            // For now, return empty list to satisfy compiler
            return await Task.FromResult(new List<CloudModel>());
        }
    }

    // Model class if not already defined elsewhere
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