using Microsoft.Playwright;

namespace QaDotnetWorkflows.Tests.Pages;

public class TicketListPage
{
    private readonly IPage page;

    public TicketListPage(IPage page)
    {
        this.page = page;
    }

    public Task OpenAsync() => page.GotoAsync("/#tickets");
    public Task NewTicketAsync() => page.GetByRole(AriaRole.Link, new() { Name = "New ticket", Exact = true }).ClickAsync();
    public ILocator TicketLink(string title) => page.GetByRole(AriaRole.Link, new() { Name = title, Exact = true });
    public ILocator TicketLinks => page.GetByRole(AriaRole.Table, new() { Name = "Tickets", Exact = true }).GetByRole(AriaRole.Link);

    public async Task FilterAsync(string search = "", string priority = "", string status = "")
    {
        await page.GetByLabel("Search tickets", new() { Exact = true }).FillAsync(search);
        await page.GetByLabel("Filter by priority", new() { Exact = true }).SelectOptionAsync(priority);
        await page.GetByLabel("Filter by status", new() { Exact = true }).SelectOptionAsync(status);
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply filters" }).ClickAsync();
    }
}
