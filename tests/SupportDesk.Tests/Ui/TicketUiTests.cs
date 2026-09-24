using Allure.NUnit;
using Microsoft.Playwright;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Database;
using QaDotnetWorkflows.Tests.Fixtures;
using QaDotnetWorkflows.Tests.TestData;

namespace QaDotnetWorkflows.Tests.Ui;

[TestFixture]
[AllureNUnit]
[Category("Ui")]
public class TicketUiTests : UiTest
{
    [Test]
    [Category("Smoke")]
    public async Task TicketCanBeCreatedThroughTheForm()
    {
        var ticket = TicketData.NewTicket("High");
        await Tickets.OpenAsync();
        await Tickets.NewTicketAsync();
        Data.Track(await Form.CreateAsync(ticket));

        await Expect(Details.Heading).ToHaveTextAsync(ticket.Title);
        await Expect(Details.Description).ToHaveTextAsync(ticket.Description);
        await Expect(Details.Priority).ToHaveTextAsync("High");
        await Expect(Details.Status).ToHaveTextAsync("Open");
        await Page.ReloadAsync();
        await Expect(Details.Heading).ToHaveTextAsync(ticket.Title);
    }

    [Test]
    public async Task BlankTitleShowsValidationAndKeepsTheFormOpen()
    {
        var request = TicketData.NewTicket();
        request.Title = "   ";
        request.Description = $"UI validation {Guid.NewGuid():N}";
        await Tickets.OpenAsync();
        await Tickets.NewTicketAsync();
        await Form.FillAsync(request);
        await Form.SaveAsync();

        await Expect(Form.TitleError).ToBeVisibleAsync();
        await Expect(Form.TitleError).ToHaveTextAsync("Title must contain 1 to 120 characters.");
        await Expect(Form.Title).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "New ticket", Exact = true })).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("#new$"));
        var database = new TicketDatabase(Settings.DatabaseConnection);
        Assert.That(await database.CountByDescriptionAsync(request.Description), Is.Zero);
    }

    [Test]
    public async Task CancellingAnEditPreservesSavedValues()
    {
        var ticket = TicketData.NewTicket("Low");
        await Tickets.OpenAsync();
        await Tickets.NewTicketAsync();
        Data.Track(await Form.CreateAsync(ticket));
        await Expect(Details.Heading).ToHaveTextAsync(ticket.Title);

        await Details.EditAsync();
        await Form.FillAsync(TicketData.NewTicket("High"));
        await Form.CancelAsync();
        await Page.ReloadAsync();

        await Expect(Details.Heading).ToHaveTextAsync(ticket.Title);
        await Expect(Details.Description).ToHaveTextAsync(ticket.Description);
        await Expect(Details.Priority).ToHaveTextAsync("Low");
    }
}
