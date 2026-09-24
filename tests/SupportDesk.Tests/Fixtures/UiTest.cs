using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using QaDotnetWorkflows.Tests.Clients;
using QaDotnetWorkflows.Tests.Configuration;
using QaDotnetWorkflows.Tests.Pages;
using QaDotnetWorkflows.Tests.Reporting;

namespace QaDotnetWorkflows.Tests.Fixtures;

public class UiTest : PageTest
{
    private HttpClient httpClient = null!;
    private bool tracingStarted;
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
    public async Task SetUpUiAsync()
    {
        Settings = new TestSettings();
        httpClient = new HttpClient { BaseAddress = new Uri(Settings.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        Api = new TicketsApiClient(httpClient);
        Data = new TicketTestData(Api);
        Tickets = new TicketListPage(Page);
        Form = new TicketFormPage(Page);
        Details = new TicketDetailsPage(Page);
        Page.SetDefaultTimeout(Settings.ActionTimeoutMs);
        SetDefaultExpectTimeout(Settings.ExpectTimeoutMs);
        await Context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = true });
        tracingStarted = true;
    }

    [TearDown]
    public async Task TearDownUiAsync()
    {
        try
        {
            if (tracingStarted)
            {
                try
                {
                    await UiDiagnostics.SaveAsync(Page, Context, Path.Combine(Settings.ArtifactsDirectory, "ui"),
                        TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed);
                }
                catch (Exception exception)
                {
                    TestContext.Error.WriteLine($"Unable to save UI diagnostics: {exception.Message}");
                }
                tracingStarted = false;
            }
        }
        finally
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
}
