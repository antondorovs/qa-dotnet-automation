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

    [Test]
    public async Task TicketCreationTimeIsConsistentAcrossResponses()
    {
        using var createdResponse = await Api.CreateAsync(TicketData.NewTicket());
        Assert.That(createdResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createdResponse.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Create response was empty.");
        Data.Track(created.Id);

        using var getResponse = await Api.GetAsync(created.Id);
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await getResponse.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(saved.CreatedAt, Is.EqualTo(created.CreatedAt));
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
    public async Task UpdatingTicketPreservesItsWorkflowAndDiscussion()
    {
        var original = await Data.CreateAsync(TicketData.NewTicket("Low"));
        using var started = await Api.ChangeStatusAsync(original.Id, "InProgress");
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var commentResponse = await Api.AddCommentAsync(original.Id, "Existing investigation notes.");
        Assert.That(commentResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var comment = await commentResponse.Content.ReadFromJsonAsync<CommentResponse>()
            ?? throw new AssertionException("Create comment response was empty.");

        var update = TicketData.NewTicket("High");
        update.Title = $"  {update.Title}  ";
        update.Description = $"  {update.Description}  ";
        using var response = await Api.UpdateAsync(original.Id, update);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Update response was empty.");

        using var savedResponse = await Api.GetAsync(original.Id);
        Assert.That(savedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await savedResponse.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        using (Assert.EnterMultipleScope())
        {
            foreach (var ticket in new[] { updated, saved })
            {
                Assert.That(ticket.Id, Is.EqualTo(original.Id));
                Assert.That(ticket.Title, Is.EqualTo(update.Title.Trim()));
                Assert.That(ticket.Description, Is.EqualTo(update.Description.Trim()));
                Assert.That(ticket.Priority, Is.EqualTo("High"));
                Assert.That(ticket.Status, Is.EqualTo("InProgress"));
                Assert.That(ticket.CreatedAt, Is.EqualTo(original.CreatedAt));
                Assert.That(ticket.CommentCount, Is.EqualTo(1));
            }
        }

        using var commentsResponse = await Api.GetCommentsAsync(original.Id);
        Assert.That(commentsResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await commentsResponse.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");
        Assert.That(comments, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(comments[0].Id, Is.EqualTo(comment.Id));
            Assert.That(comments[0].Body, Is.EqualTo(comment.Body));
            Assert.That(comments[0].CreatedAt, Is.EqualTo(comment.CreatedAt));
        }
    }

    [TestCase("title", "Title must contain 1 to 120 characters.")]
    [TestCase("description", "Description must contain at most 2000 characters.")]
    [TestCase("priority", "Priority must be Low, Normal or High.")]
    public async Task InvalidUpdatePreservesTheSavedTicket(string field, string message)
    {
        var original = await Data.CreateAsync(TicketData.NewTicket("Low"));
        var update = TicketData.NewTicket("High");
        switch (field)
        {
            case "title":
                update.Title = new string('x', 121);
                break;
            case "description":
                update.Description = new string('x', 2001);
                break;
            case "priority":
                update.Priority = "Urgent";
                break;
        }

        using var response = await Api.UpdateAsync(original.Id, update);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("errors").GetProperty(field)[0].GetString(),
            Is.EqualTo(message));

        using var savedResponse = await Api.GetAsync(original.Id);
        Assert.That(savedResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await savedResponse.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.Id, Is.EqualTo(original.Id));
            Assert.That(saved.Title, Is.EqualTo(original.Title));
            Assert.That(saved.Description, Is.EqualTo(original.Description));
            Assert.That(saved.Priority, Is.EqualTo(original.Priority));
            Assert.That(saved.Status, Is.EqualTo(original.Status));
            Assert.That(saved.CreatedAt, Is.EqualTo(original.CreatedAt));
            Assert.That(saved.CommentCount, Is.EqualTo(original.CommentCount));
        }
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
    public async Task NewestSortPlacesRecentlyCreatedTicketsFirst()
    {
        var marker = $"Creation order {Guid.NewGuid():N}";
        var olderRequest = TicketData.NewTicket();
        olderRequest.Title = $"{marker} older";
        var older = await Data.CreateAsync(olderRequest);
        await Task.Delay(10);
        var newerRequest = TicketData.NewTicket();
        newerRequest.Title = $"{marker} newer";
        var newer = await Data.CreateAsync(newerRequest);

        using var response = await Api.ListAsync(marker, "", "", "newest");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matches = await response.Content.ReadFromJsonAsync<List<TicketResponse>>()
            ?? throw new AssertionException("List response was empty.");

        Assert.That(matches.Select(match => match.Id),
            Is.EqualTo(new[] { newer.Id, older.Id }));
    }

    [Test]
    public async Task OldestSortPlacesEarlierCreatedTicketsFirst()
    {
        var marker = $"Oldest order {Guid.NewGuid():N}";
        var olderRequest = TicketData.NewTicket();
        olderRequest.Title = $"{marker} older";
        var older = await Data.CreateAsync(olderRequest);
        await Task.Delay(10);
        var newerRequest = TicketData.NewTicket();
        newerRequest.Title = $"{marker} newer";
        var newer = await Data.CreateAsync(newerRequest);

        using var response = await Api.ListAsync(marker, "", "", "oldest");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var matches = await response.Content.ReadFromJsonAsync<List<TicketResponse>>()
            ?? throw new AssertionException("List response was empty.");

        Assert.That(matches.Select(match => match.Id),
            Is.EqualTo(new[] { older.Id, newer.Id }));
    }

    [Test]
    public async Task UnsupportedSortIsRejected()
    {
        using var response = await Api.ListAsync("", "", "", "recent");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errors").GetProperty("sort")[0].GetString(),
            Is.EqualTo("Sort must be priority, status, comments, newest or oldest."));
    }

    [TestCase("urgent", "", "priority", "Priority must be Low, Normal or High.")]
    [TestCase("", "waiting", "status", "Status must be Open, InProgress or Resolved.")]
    public async Task UnsupportedFilterIsRejected(
        string priority,
        string status,
        string field,
        string message)
    {
        using var response = await Api.ListAsync("", priority, status);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(body.RootElement.GetProperty("errors").GetProperty(field)[0].GetString(),
            Is.EqualTo(message));
    }

    [Test]
    public async Task DeletingTicketRemovesOnlyItsOwnCommentsFromDatabase()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        var other = await Data.CreateAsync(TicketData.NewTicket());
        using var first = await Api.AddCommentAsync(ticket.Id, "First note to remove.");
        using var second = await Api.AddCommentAsync(ticket.Id, "Second note to remove.");
        using var retained = await Api.AddCommentAsync(other.Id, "This discussion must remain.");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(retained.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var database = new TicketDatabase(Settings.DatabaseConnection);
        Assert.That(await database.CountCommentsAsync(ticket.Id), Is.EqualTo(2));
        Assert.That(await database.CountCommentsAsync(other.Id), Is.EqualTo(1));

        using var deleted = await Api.DeleteAsync(ticket.Id);
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var missingTicket = await Api.GetAsync(ticket.Id);
        using var missingComments = await Api.GetCommentsAsync(ticket.Id);
        Assert.That(missingTicket.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(missingComments.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await database.CountCommentsAsync(ticket.Id), Is.Zero);
        Assert.That(await database.CountCommentsAsync(other.Id), Is.EqualTo(1));

        using var otherComments = await Api.GetCommentsAsync(other.Id);
        Assert.That(otherComments.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await otherComments.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");
        Assert.That(comments.Select(comment => comment.Body),
            Is.EqualTo(new[] { "This discussion must remain." }));
    }

    [Test]
    public async Task TicketReportsItsCommentCount()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        using var first = await Api.AddCommentAsync(ticket.Id, "Customer confirmed the issue.");
        using var second = await Api.AddCommentAsync(ticket.Id, "Investigation has started.");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var response = await Api.GetAsync(ticket.Id);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await response.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");

        Assert.That(saved.CommentCount, Is.EqualTo(2));
    }

    public static IEnumerable<TestCaseData> InvalidComments()
    {
        yield return new TestCaseData((string?)null).SetName("NullCommentIsRejected");
        yield return new TestCaseData("").SetName("EmptyCommentIsRejected");
        yield return new TestCaseData(" \t\r\n ").SetName("WhitespaceCommentIsRejected");
        yield return new TestCaseData(new string('x', 2001)).SetName("OversizedCommentIsRejected");
    }

    [TestCaseSource(nameof(InvalidComments))]
    public async Task InvalidCommentDoesNotChangeSavedComments(string? comment)
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        using var existingResponse = await Api.AddCommentAsync(ticket.Id, "Existing discussion.");
        Assert.That(existingResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var existing = await existingResponse.Content.ReadFromJsonAsync<CommentResponse>()
            ?? throw new AssertionException("Create comment response was empty.");

        using var response = await Api.AddCommentAsync(ticket.Id, comment);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(error.RootElement.GetProperty("errors").GetProperty("body")[0].GetString(),
            Is.EqualTo("Comment must contain 1 to 2000 characters."));

        using var listResponse = await Api.GetCommentsAsync(ticket.Id);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await listResponse.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");
        using var ticketResponse = await Api.GetAsync(ticket.Id);
        Assert.That(ticketResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await ticketResponse.Content.ReadFromJsonAsync<TicketResponse>()
            ?? throw new AssertionException("Get response was empty.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(comments.Select(item => item.Id), Is.EqualTo(new[] { existing.Id }));
            Assert.That(comments[0].Body, Is.EqualTo("Existing discussion."));
            Assert.That(saved.CommentCount, Is.EqualTo(1));
        }
    }

    [TestCase(1)]
    [TestCase(2000)]
    public async Task CommentLengthBoundariesAreAcceptedAfterTrimming(int length)
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        var body = new string('x', length);
        using var response = await Api.AddCommentAsync(ticket.Id, $" \t{body}\r\n ");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<CommentResponse>()
            ?? throw new AssertionException("Create comment response was empty.");

        using var listResponse = await Api.GetCommentsAsync(ticket.Id);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await listResponse.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(created.Body, Is.EqualTo(body));
            Assert.That(comments.Select(item => item.Id), Is.EqualTo(new[] { created.Id }));
            Assert.That(comments[0].Body, Is.EqualTo(body));
        }
    }

    [Test]
    public async Task CommentTimestampIsConsistentAcrossResponses()
    {
        var ticket = await Data.CreateAsync(TicketData.NewTicket());
        using var createdResponse = await Api.AddCommentAsync(ticket.Id, "Timestamp contract check.");
        Assert.That(createdResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createdResponse.Content.ReadFromJsonAsync<CommentResponse>()
            ?? throw new AssertionException("Create comment response was empty.");

        using var listResponse = await Api.GetCommentsAsync(ticket.Id);
        Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var comments = await listResponse.Content.ReadFromJsonAsync<List<CommentResponse>>()
            ?? throw new AssertionException("Comments response was empty.");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
            Assert.That(comments, Has.Count.EqualTo(1));
            Assert.That(comments[0].CreatedAt, Is.EqualTo(created.CreatedAt));
        }
    }
}
