using Microsoft.Playwright;
using QaDotnetWorkflows.Tests.Models;

namespace QaDotnetWorkflows.Tests.Pages;

public class TicketFormPage
{
    private readonly IPage page;

    public TicketFormPage(IPage page)
    {
        this.page = page;
    }

    public ILocator Title => page.GetByLabel("Title", new() { Exact = true });
    public ILocator TitleError => page.Locator("#title-error");

    public async Task FillAsync(CreateTicketRequest ticket)
    {
        await Title.FillAsync(ticket.Title);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync(ticket.Description);
        await page.GetByLabel("Priority", new() { Exact = true }).SelectOptionAsync(ticket.Priority);
    }

    public Task SaveAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Save ticket" }).ClickAsync();
    public Task CancelAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();

    public async Task<Guid> CreateAsync(CreateTicketRequest ticket)
    {
        await FillAsync(ticket);
        var response = await page.RunAndWaitForResponseAsync(SaveAsync,
            response => response.Request.Method == "POST" && new Uri(response.Url).AbsolutePath == "/api/tickets");
        var body = await response.JsonAsync()
            ?? throw new InvalidOperationException("Create response was empty.");
        return body.GetProperty("id").GetGuid();
    }
}
