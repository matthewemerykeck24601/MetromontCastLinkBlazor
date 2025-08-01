// Add these methods to the existing StorageService class:

using MetromontCastLink.Shared.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

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
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
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
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
            var data = JsonSerializer.Deserialize<dynamic>(json);
            return data?.calculation as CalculationResult;
        }

        // If not in local storage, try OSS
        var token = await _accService.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/oss-storage/calculation/{calculationId}");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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