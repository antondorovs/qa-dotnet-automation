using System.Net;
using Allure.NUnit;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Fixtures;
using QaDotnetWorkflows.Tests.TestData;

namespace QaDotnetWorkflows.Tests.Workflows;

[TestFixture]
[AllureNUnit]
[Category("Ui")]
[Category("Workflow")]
public class TicketDiscoveryTests : UiTest
{
    [Test]
    [Category("Smoke")]
    public async Task ApiCreatedTicketAppearsInListAndDetails()
    {
        var request = TicketData.NewTicket("High");
        var ticket = await Data.CreateAsync(request);

        await Tickets.OpenAsync();
        await Expect(Tickets.TicketLink(request.Title)).ToBeVisibleAsync();
        await Tickets.TicketLink(request.Title).ClickAsync();

        await Expect(Page).ToHaveURLAsync($"{Settings.BaseUrl}/#tickets/{ticket.Id}");
        await Expect(Details.Heading).ToHaveTextAsync(request.Title);
        await Expect(Details.Description).ToHaveTextAsync(request.Description);
        await Expect(Details.Priority).ToHaveTextAsync("High");
        await Expect(Details.Status).ToHaveTextAsync("Open");
    }

    [Test]
    public async Task SearchFindsTheMatchingTicketAndExcludesOthers()
    {
        var request = TicketData.NewTicket();
        request.Title = $"Refund {Guid.NewGuid():N}";
        await Data.CreateAsync(request);
        var unrelated = await Data.CreateAsync(TicketData.NewTicket());

        await Tickets.OpenAsync();
        await Tickets.FilterAsync(request.Title.ToUpperInvariant());

        await Expect(Tickets.TicketLinks).ToHaveTextAsync(new[] { request.Title });
        await Expect(Tickets.TicketLink(unrelated.Title)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SearchFindsTextInTheTicketDescription()
    {
        var marker = $"account-{Guid.NewGuid():N}";
        var request = TicketData.NewTicket();
        request.Description = $"Customer cannot access {marker}";
        await Data.CreateAsync(request);

        await Tickets.OpenAsync();
        await Tickets.FilterAsync($"  {marker.ToUpperInvariant()}  ");

        await Expect(Tickets.TicketLinks).ToHaveTextAsync(new[] { request.Title });
    }

    [Test]
    public async Task PriorityAndStatusFiltersApplyTogether()
    {
        var prefix = $"Queue {Guid.NewGuid():N}";
        var highRequest = TicketData.NewTicket("High");
        highRequest.Title = $"{prefix} active high";
        var expected = await Data.CreateAsync(highRequest);
        using var activeHigh = await Api.ChangeStatusAsync(expected.Id, "InProgress");
        Assert.That(activeHigh.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var openRequest = TicketData.NewTicket("High");
        openRequest.Title = $"{prefix} open high";
        await Data.CreateAsync(openRequest);

        var lowRequest = TicketData.NewTicket("Low");
        lowRequest.Title = $"{prefix} active low";
        var low = await Data.CreateAsync(lowRequest);
        using var activeLow = await Api.ChangeStatusAsync(low.Id, "InProgress");
        Assert.That(activeLow.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await Tickets.OpenAsync();
        await Tickets.FilterAsync(prefix, "High", "InProgress");

        await Expect(Tickets.TicketLinks).ToHaveTextAsync(new[] { expected.Title });
    }

    [Test]
    public async Task StatusSortDisplaysTicketsInWorkflowOrder()
    {
        var prefix = $"Workflow order {Guid.NewGuid():N}";

        var resolvedRequest = TicketData.NewTicket();
        resolvedRequest.Title = $"{prefix} A resolved";
        var resolved = await Data.CreateAsync(resolvedRequest);
        using var resolvedStarted = await Api.ChangeStatusAsync(resolved.Id, "InProgress");
        Assert.That(resolvedStarted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var resolvedCompleted = await Api.ChangeStatusAsync(resolved.Id, "Resolved");
        Assert.That(resolvedCompleted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var activeRequest = TicketData.NewTicket();
        activeRequest.Title = $"{prefix} B active";
        var active = await Data.CreateAsync(activeRequest);
        using var activeStarted = await Api.ChangeStatusAsync(active.Id, "InProgress");
        Assert.That(activeStarted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var openRequest = TicketData.NewTicket();
        openRequest.Title = $"{prefix} C open";
        var open = await Data.CreateAsync(openRequest);

        await Tickets.OpenAsync();
        await Tickets.FilterAsync(prefix, sort: "status");

        await Expect(Tickets.TicketLinks).ToHaveTextAsync(new[]
        {
            open.Title,
            active.Title,
            resolved.Title
        });
    }

    [Test]
    public async Task ClearingFiltersRestoresHiddenTickets()
    {
        var visible = await Data.CreateAsync(TicketData.NewTicket("High"));
        var hidden = await Data.CreateAsync(TicketData.NewTicket("Low"));

        await Tickets.OpenAsync();
        await Tickets.FilterAsync(visible.Title, "High", "Open", "priority");
        await Expect(Tickets.TicketLinks).ToHaveTextAsync(new[] { visible.Title });

        await Tickets.ClearFiltersAsync();

        await Expect(Tickets.TicketLink(visible.Title)).ToBeVisibleAsync();
        await Expect(Tickets.TicketLink(hidden.Title)).ToBeVisibleAsync();
    }
}
