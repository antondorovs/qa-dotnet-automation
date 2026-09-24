using System.Text;
using System.Text.Json;
using Allure.Net.Commons;

namespace QaDotnetWorkflows.Tests.Reporting;

public static class ApiDiagnostics
{
    public static async Task<HttpResponseMessage> AttachAsync(Task<HttpResponseMessage> responseTask)
    {
        var response = await responseTask;
        try
        {
            var request = response.RequestMessage;
            var details = new
            {
                method = request?.Method.Method,
                path = request?.RequestUri?.AbsolutePath,
                status = (int)response.StatusCode,
                requestBody = request?.Content is null ? null : await request.Content.ReadAsStringAsync(),
                responseBody = await response.Content.ReadAsStringAsync()
            };
            var json = JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true });
            AllureApi.AddAttachment($"{details.method} {details.path}", "application/json", Encoding.UTF8.GetBytes(json), ".json");
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }
}
