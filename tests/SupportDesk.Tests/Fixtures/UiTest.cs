using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using QaDotnetWorkflows.Tests.Clients;
using QaDotnetWorkflows.Tests.Configuration;
using QaDotnetWorkflows.Tests.Pages;

namespace QaDotnetWorkflows.Tests.Fixtures;

public class UiTest : PageTest
{
    private HttpClient httpClient = null!;
    protected TestSettings Settings { get; private set; } = null!;
    protected TicketsApiClient Api { get; private set; } = null!;
    protected TicketTestData Data { get; private set; } = null!;
    protected TicketListPage Tickets { get; private set; } = null!;
    protected TicketFormPage Form { get; private set; } = null!;
    protected TicketDetailsPage Details { get; private set; } = null!;

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            BaseURL = new TestSettings().BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        };
    }

    [SetUp]
    public void SetUpUi()
    {
        Settings = new TestSettings();
        httpClient = new HttpClient { BaseAddress = new Uri(Settings.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        Api = new TicketsApiClient(httpClient);
        Data = new TicketTestData(Api);
        Tickets = new TicketListPage(Page);
        Form = new TicketFormPage(Page);
        Details = new TicketDetailsPage(Page);
        Page.SetDefaultTimeout(10000);
        SetDefaultExpectTimeout(5000);
    }

    [TearDown]
    public async Task TearDownUiAsync()
    {
        try
        {
            if (Data is not null)
            {
                await Data.CleanupAsync();
            }
        }
        finally
        {
            httpClient?.Dispose();
        }
    }
}
