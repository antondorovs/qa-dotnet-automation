using System.Net;
using System.Net.Http.Json;
using Microsoft.Playwright;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Database;
using QaDotnetWorkflows.Tests.Fixtures;
using QaDotnetWorkflows.Tests.Models;
using QaDotnetWorkflows.Tests.TestData;

namespace QaDotnetWorkflows.Tests.Workflows;

[TestFixture]
[Category("Ui")]
[Category("Workflow")]
public class TicketActionTests : UiTest
{
    [Test]
    public async Task PriorityChangedInUiIsReturnedByApi()
    {
        var request = TicketData.NewTicket("Low");
        var ticket = await Data.CreateAsync(request);
        await Details.OpenAsync(ticket.Id);
        await Expect(Details.Priority).ToHaveTextAsync("Low");

        await Details.EditAsync();
        request.Priority = "High";
        await Form.FillAsync(request);
        await Form.SaveAsync();
        await Expect(Details.Priority).ToHaveTextAsync("High");

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Priority, Is.EqualTo("High"));
            Assert.That(saved.Title, Is.EqualTo(ticket.Title));
            Assert.That(saved.Description, Is.EqualTo(ticket.Description));
            Assert.That(saved.Status, Is.EqualTo("Open"));
        }
    }

    [Test]
    [Category("Smoke")]
    public async Task StartingWorkUpdatesApiAndDatabase()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        await Details.OpenAsync(ticket.Id);
        await Expect(Details.Status).ToHaveTextAsync("Open");

        await Details.StartWorkAsync();
        await Expect(Details.Status).ToHaveTextAsync("InProgress");

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        Assert.That(saved.Status, Is.EqualTo("InProgress"));
        var database = new TicketDatabase(Settings.DatabaseConnection);
        Assert.That(await database.GetStatusAsync(ticket.Id), Is.EqualTo("InProgress"));
    }

    [Test]
    public async Task ResolvingTicketPreservesTheUiCommentInApi()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        using var prepared = await Api.ChangeStatusAsync(ticket.Id, "InProgress");
        Assert.That(prepared.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await Details.OpenAsync(ticket.Id);
        await Expect(Details.Status).ToHaveTextAsync("InProgress");

        const string comment = "Delivery date confirmed with the customer.";
        await Details.AddCommentAsync(comment);
        await Expect(Details.Comments).ToHaveTextAsync(new[] { comment });
        await Details.ResolveAsync();
        await Expect(Details.Status).ToHaveTextAsync("Resolved");

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        Assert.That(saved.Status, Is.EqualTo("Resolved"));

        using var commentsResponse = await Api.GetCommentsAsync(ticket.Id);
        Assert.That(commentsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await commentsResponse.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");
        Assert.That(comments, Has.Count.EqualTo(1));
        Assert.That(comments[0].Id, Is.Not.EqualTo(Guid.Empty));
        Assert.That(comments[0].Body, Is.EqualTo(comment));
    }

    [Test]
    public async Task DeletedTicketDisappearsFromUiAndApi()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        await Details.OpenAsync(ticket.Id);
        await Expect(Details.Heading).ToHaveTextAsync(ticket.Title);

        await Details.DeleteAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Tickets", Exact = true })).ToBeVisibleAsync();
        await Expect(Tickets.TicketLink(ticket.Title)).ToHaveCountAsync(0);

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
