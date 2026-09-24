using NUnit.Framework;
using QaDotnetWorkflows.Tests.Clients;
using QaDotnetWorkflows.Tests.Configuration;

namespace QaDotnetWorkflows.Tests.Fixtures;

public class ApiTest
{
    private HttpClient httpClient = null!;
    protected TestSettings Settings { get; private set; } = null!;
    protected TicketsApiClient Api { get; private set; } = null!;
    protected TicketTestData Data { get; private set; } = null!;

    [SetUp]
    public void SetUpApi()
    {
        Settings = new TestSettings();
        httpClient = new HttpClient { BaseAddress = new Uri(Settings.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(15) };
        Api = new TicketsApiClient(httpClient);
        Data = new TicketTestData(Api);
    }

    [TearDown]
    public async Task TearDownApiAsync()
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
