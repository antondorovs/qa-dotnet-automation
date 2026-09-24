using Microsoft.Extensions.Configuration;

namespace QaDotnetWorkflows.Tests.Configuration;

public class TestSettings
{
    public string BaseUrl { get; }
    public string ApiBaseUrl { get; }
    public string DatabaseConnection { get; }

    public TestSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.json")
            .AddEnvironmentVariables("QA_")
            .Build();
        BaseUrl = configuration["BaseUrl"] ?? throw new InvalidOperationException("QA_BaseUrl is required.");
        ApiBaseUrl = configuration["ApiBaseUrl"] ?? BaseUrl;
        DatabaseConnection = configuration["DatabaseConnection"] ?? "";
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("BaseUrl and ApiBaseUrl must be absolute URLs.");
        }
    }
}
