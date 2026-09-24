using Microsoft.Extensions.Configuration;

namespace QaDotnetWorkflows.Tests.Configuration;

public class TestSettings
{
    public string BaseUrl { get; }
    public string ApiBaseUrl { get; }
    public string DatabaseConnection { get; }
    public string ArtifactsDirectory { get; }
    public int ActionTimeoutMs { get; }
    public int ExpectTimeoutMs { get; }

    public TestSettings()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("testsettings.json")
            .AddEnvironmentVariables("QA_")
            .Build();
        BaseUrl = (configuration["BaseUrl"] ?? throw new InvalidOperationException("QA_BaseUrl is required.")).TrimEnd('/');
        ApiBaseUrl = (configuration["ApiBaseUrl"] ?? BaseUrl).TrimEnd('/');
        DatabaseConnection = configuration["DatabaseConnection"] ?? "";
        ArtifactsDirectory = Path.GetFullPath(configuration["ArtifactsDirectory"] ?? "../../../../../artifacts", AppContext.BaseDirectory);
        ActionTimeoutMs = ReadTimeout(configuration, "ActionTimeoutMs", 10000);
        ExpectTimeoutMs = ReadTimeout(configuration, "ExpectTimeoutMs", 5000);
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) ||
            !Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("BaseUrl and ApiBaseUrl must be absolute URLs.");
        }
    }

    private static int ReadTimeout(IConfiguration configuration, string name, int defaultValue)
    {
        var value = configuration[name];
        if (value is null)
        {
            return defaultValue;
        }
        if (!int.TryParse(value, out var timeout) || timeout <= 0)
        {
            throw new InvalidOperationException($"QA_{name} must be a positive integer.");
        }
        return timeout;
    }
}
