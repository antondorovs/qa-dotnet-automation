using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Allure.NUnit;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Database;
using QaDotnetWorkflows.Tests.Fixtures;
using QaDotnetWorkflows.Tests.Models;
using QaDotnetWorkflows.Tests.TestData;

namespace QaDotnetWorkflows.Tests.Api;

[TestFixture]
[AllureNUnit]
[Category("Api")]
public class TicketApiTests : ApiTest
{
    public static IEnumerable<TestCaseData> InvalidTickets()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "invalid-tickets.json");
        var cases = JsonSerializer.Deserialize<List<InvalidTicketCase>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException("Invalid ticket cases were empty.");
        var tests = new List<TestCaseData>();
        foreach (var testCase in cases)
        {
            tests.Add(new TestCaseData(testCase).SetName(testCase.Name));
        }
        return tests;
    }

    [Test]
    [Category("Smoke")]
    public async Task CreatedTicketCanBeReadBack()
    {
        var request = TicketData.NewTicket("High");
        using var created = await Api.CreateAsync(request);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var ticket = await created.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Create response was empty.");
        Data.Track(ticket.Id);
        Assert.That(ticket.Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(created.Headers.Location?.OriginalString, Is.EqualTo($"/api/tickets/{ticket.Id}"));

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Id, Is.EqualTo(ticket.Id));
            Assert.That(saved.Title, Is.EqualTo(request.Title));
            Assert.That(saved.Description, Is.EqualTo(request.Description));
            Assert.That(saved.Priority, Is.EqualTo("High"));
            Assert.That(saved.Status, Is.EqualTo("Open"));
        }
    }

    [TestCaseSource(nameof(InvalidTickets))]
    public async Task InvalidTicketIsRejected(InvalidTicketCase testCase)
    {
        var request = new CreateTicketRequest
        {
            Title = testCase.Title,
            Description = $"Invalid ticket {Guid.NewGuid():N}",
            Priority = testCase.Priority
        };
        using var response = await Api.CreateAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var unexpected = await response.Content.ReadFromJsonAsync<TicketResponse>();
            if (unexpected is not null)
            {
                Data.Track(unexpected.Id);
            }
        }
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errors").GetProperty(testCase.ErrorField)[0].GetString(),
            Is.EqualTo(testCase.ErrorMessage));
        var database = new TicketDatabase(Settings.DatabaseConnection);
        Assert.That(await database.CountByDescriptionAsync(request.Description), Is.Zero);
    }

    [Test]
    public async Task UnknownTicketReturnsNotFound()
    {
        using var response = await Api.GetAsync(Guid.NewGuid());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("error").GetString(), Is.EqualTo("Ticket not found."));
    }

    [Test]
    public async Task OpenTicketCannotSkipInProgress()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        using var changed = await Api.ChangeStatusAsync(ticket.Id, "Resolved");
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using var error = JsonDocument.Parse(await changed.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("error").GetString(), Is.EqualTo("Status transition is not allowed."));

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        Assert.That(saved.Status, Is.EqualTo("Open"));
    }

    [Test]
    public async Task FiltersIgnoreCaseAndSurroundingSpaces()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket("High"));
        using var changed = await Api.ChangeStatusAsync(ticket.Id, "InProgress");
        Assert.That(changed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var response = await Api.ListAsync(ticket.Title, "  high  ", "  inprogress  ");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matches = await response.Content.ReadFromJsonAsync<List<TicketResponse>>()
            ?? throw new AssertionException("List response was empty.");

        Assert.That(matches.Select(match => match.Id), Is.EqualTo(new[] { ticket.Id }));
    }

    [Test]
    public async Task PrioritySortPlacesUrgentTicketsFirst()
    {
        var marker = $"Priority order {Guid.NewGuid():N}";
        foreach (var priority in new[] { "Low", "High", "Normal" })
        {
            var request = TicketData.NewTicket(priority);
            request.Title = $"{marker} {priority}";
            await Data.CreateAsync(request);
        }

        using var response = await Api.ListAsync(marker, "", "", "priority");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matches = await response.Content.ReadFromJsonAsync<List<TicketResponse>>()
            ?? throw new AssertionException("List response was empty.");

        Assert.That(matches.Select(match => match.Priority),
            Is.EqualTo(new[] { "High", "Normal", "Low" }));
    }

    [Test]
    public async Task StatusSortFollowsTheWorkflowOrder()
    {
        var marker = $"Status order {Guid.NewGuid():N}";
        var open = TicketData.NewTicket();
        open.Title = $"{marker} open";
        await Data.CreateAsync(open);

        var active = TicketData.NewTicket();
        active.Title = $"{marker} active";
        var activeTicket = await Data.CreateAsync(active);
        using var activated = await Api.ChangeStatusAsync(activeTicket.Id, "InProgress");
        Assert.That(activated.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var resolved = TicketData.NewTicket();
        resolved.Title = $"{marker} resolved";
        var resolvedTicket = await Data.CreateAsync(resolved);
        using var started = await Api.ChangeStatusAsync(resolvedTicket.Id, "InProgress");
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var completed = await Api.ChangeStatusAsync(resolvedTicket.Id, "Resolved");
        Assert.That(completed.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var response = await Api.ListAsync(marker, "", "", "status");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matches = await response.Content.ReadFromJsonAsync<List<TicketResponse>>()
            ?? throw new AssertionException("List response was empty.");

        Assert.That(matches.Select(match => match.Status),
            Is.EqualTo(new[] { "Open", "InProgress", "Resolved" }));
    }

    [Test]
    public async Task UnsupportedSortIsRejected()
    {
        using var response = await Api.ListAsync("", "", "", "newest");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errors").GetProperty("sort")[0].GetString(),
            Is.EqualTo("Sort must be priority or status."));
    }
}
