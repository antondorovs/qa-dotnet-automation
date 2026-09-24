using Microsoft.Playwright;

namespace QaDotnetWorkflows.Tests.Pages;

public class TicketDetailsPage
{
    private readonly IPage page;

    public TicketDetailsPage(IPage page)
    {
        this.page = page;
    }

    public ILocator Heading => page.Locator("#detail-view").GetByRole(AriaRole.Heading, new() { Level = 1 });
    public ILocator Description => page.GetByTestId("ticket-description");
    public ILocator Priority => page.GetByTestId("ticket-priority");
    public ILocator Status => page.GetByTestId("ticket-status");
    public Task OpenAsync(Guid id) => page.GotoAsync($"/#tickets/{id}");
    public Task EditAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Edit ticket" }).ClickAsync();
    public Task StartWorkAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Start work" }).ClickAsync();
    public Task ResolveAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Resolve ticket" }).ClickAsync();
    public ILocator Comments => page.GetByRole(AriaRole.List, new() { Name = "Comments" }).GetByRole(AriaRole.Listitem);

    public async Task AddCommentAsync(string body)
    {
        await page.GetByLabel("Comment", new() { Exact = true }).FillAsync(body);
        await page.GetByRole(AriaRole.Button, new() { Name = "Add comment" }).ClickAsync();
    }

    public async Task DeleteAsync()
    {
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete ticket" }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Confirm delete" }).ClickAsync();
    }
}
