using Allure.Net.Commons;
using Microsoft.Playwright;
using NUnit.Framework;

namespace QaDotnetWorkflows.Tests.Reporting;

public static class UiDiagnostics
{
    public static async Task SaveAsync(IPage page, IBrowserContext context, string directory, bool failed)
    {
        if (!failed)
        {
            await context.Tracing.StopAsync();
            return;
        }

        Directory.CreateDirectory(directory);
        var name = $"{TestContext.CurrentContext.Test.MethodName}-{Guid.NewGuid():N}";
        var screenshotPath = Path.Combine(directory, $"{name}.png");
        var tracePath = Path.Combine(directory, $"{name}.zip");
        try
        {
            if (!page.IsClosed)
            {
                await page.ScreenshotAsync(new() { Path = screenshotPath, FullPage = true, Timeout = 5000 });
                AllureApi.AddAttachment("Failure screenshot", "image/png", screenshotPath);
                TestContext.AddTestAttachment(screenshotPath);
            }
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = tracePath });
            AllureApi.AddAttachment("Playwright trace", "application/zip", tracePath);
            TestContext.AddTestAttachment(tracePath);
        }
    }
}
