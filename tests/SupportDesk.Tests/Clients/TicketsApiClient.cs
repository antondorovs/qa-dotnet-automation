using System.Net.Http.Json;
using QaDotnetWorkflows.Tests.Models;
using QaDotnetWorkflows.Tests.Reporting;

namespace QaDotnetWorkflows.Tests.Clients;

public class TicketsApiClient
{
    private readonly HttpClient httpClient;

    public TicketsApiClient(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public Task<HttpResponseMessage> CreateAsync(CreateTicketRequest request)
    {
        return ApiDiagnostics.AttachAsync(httpClient.PostAsJsonAsync("/api/tickets", request));
    }

    public Task<HttpResponseMessage> GetAsync(Guid id)
    {
        return ApiDiagnostics.AttachAsync(httpClient.GetAsync($"/api/tickets/{id}"));
    }

    public Task<HttpResponseMessage> ListAsync(string search, string priority, string status, string sort = "")
    {
        var query = $"search={Uri.EscapeDataString(search)}" +
            $"&priority={Uri.EscapeDataString(priority)}" +
            $"&status={Uri.EscapeDataString(status)}" +
            $"&sort={Uri.EscapeDataString(sort)}";
        return ApiDiagnostics.AttachAsync(httpClient.GetAsync($"/api/tickets?{query}"));
    }

    public Task<HttpResponseMessage> DeleteAsync(Guid id)
    {
        return ApiDiagnostics.AttachAsync(httpClient.DeleteAsync($"/api/tickets/{id}"));
    }

    public Task<HttpResponseMessage> ChangeStatusAsync(Guid id, string status)
    {
        return ApiDiagnostics.AttachAsync(httpClient.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status }));
    }

    public Task<HttpResponseMessage> GetCommentsAsync(Guid id)
    {
        return ApiDiagnostics.AttachAsync(httpClient.GetAsync($"/api/tickets/{id}/comments"));
    }
}
