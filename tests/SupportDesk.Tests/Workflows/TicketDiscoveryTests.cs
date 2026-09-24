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
}
