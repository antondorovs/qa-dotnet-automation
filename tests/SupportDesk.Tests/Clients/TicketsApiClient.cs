using System.Net.Http.Json;
using QaDotnetWorkflows.Tests.Models;

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
        return httpClient.PostAsJsonAsync("/api/tickets", request);
    }

    public Task<HttpResponseMessage> GetAsync(Guid id)
    {
        return httpClient.GetAsync($"/api/tickets/{id}");
    }

    public Task<HttpResponseMessage> DeleteAsync(Guid id)
    {
        return httpClient.DeleteAsync($"/api/tickets/{id}");
    }

    public Task<HttpResponseMessage> ChangeStatusAsync(Guid id, string status)
    {
        return httpClient.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status });
    }
}
