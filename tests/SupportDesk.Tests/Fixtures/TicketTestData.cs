using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Clients;
using QaDotnetWorkflows.Tests.Models;

namespace QaDotnetWorkflows.Tests.Fixtures;

public class TicketTestData
{
    private readonly TicketsApiClient api;
    private readonly HashSet<Guid> ticketIds = new();

    public TicketTestData(TicketsApiClient api)
    {
        this.api = api;
    }

    public void Track(Guid id)
    {
        ticketIds.Add(id);
    }

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest request)
    {
        using var response = await api.CreateAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), "API test data preparation failed.");
        var ticket = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new InvalidOperationException("Create response was empty.");
        Track(ticket.Id);
        return ticket;
    }

    public async Task CleanupAsync()
    {
        var failures = new List<string>();
        foreach (var id in ticketIds)
        {
            try
            {
                using var response = await api.DeleteAsync(id);
                if (response.StatusCode != HttpStatusCode.NoContent && response.StatusCode != HttpStatusCode.NotFound)
                {
                    failures.Add($"{id}: {response.StatusCode}");
                }
            }
            catch (HttpRequestException exception)
            {
                failures.Add($"{id}: {exception.Message}");
            }
            catch (TaskCanceledException exception)
            {
                failures.Add($"{id}: {exception.Message}");
            }
        }
        Assert.That(failures, Is.Empty, "Could not clean up test tickets.");
    }
}
