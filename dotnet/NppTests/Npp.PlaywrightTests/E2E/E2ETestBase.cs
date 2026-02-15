using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Npp.PlaywrightTests.E2E;

public abstract class E2ETestBase : PlaywrightTest
{
    private string _testName = string.Empty;
    private string _testArtifactPrefix = string.Empty;
    private string _videoDir = string.Empty;
    private string _imageDir = string.Empty;
    private bool _setUpCompleted;
    protected IPage Page { get; private set; } = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;

    [SetUp]
    public async Task SetUpBrowser()
    {
        _setUpCompleted = false;
        _testName = Sanitize(TestContext.CurrentContext.Test.Name);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");

        _testArtifactPrefix = $"{_testName}_{stamp}";

        _videoDir = Path.Combine(TestSettings.PlaywrightArtifactsDir, "videos");
        Directory.CreateDirectory(_videoDir);
        _imageDir = Path.Combine(TestSettings.PlaywrightArtifactsDir, "images");
        Directory.CreateDirectory(_imageDir);

        _browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = TestSettings.PlaywrightHeadless,
            SlowMo = TestSettings.PlaywrightSlowMoMs
        });

        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = TestSettings.PlaywrightViewportWidth,
                Height = TestSettings.PlaywrightViewportHeight
            },
            ColorScheme = TestSettings.PlaywrightColorScheme,
            RecordVideoDir = _videoDir,
            RecordVideoSize = new RecordVideoSize
            {
                Width = TestSettings.PlaywrightViewportWidth,
                Height = TestSettings.PlaywrightViewportHeight
            }
        });

        Page = await _context.NewPageAsync();
        await Page.EmulateMediaAsync(new PageEmulateMediaOptions
        {
            ColorScheme = TestSettings.PlaywrightColorScheme
        });

        _setUpCompleted = true;
    }

    protected static ILocatorAssertions Expect(ILocator locator) => Microsoft.Playwright.Assertions.Expect(locator);

    protected static IPageAssertions Expect(IPage page) => Microsoft.Playwright.Assertions.Expect(page);

    [TearDown]
    public async Task SaveArtifacts()
    {
        Task<string>? videoPathTask = null;

        try
        {
            if (Page?.Video != null)
            {
                videoPathTask = Page.Video.PathAsync();
            }

            if (Page != null && !string.IsNullOrWhiteSpace(_imageDir))
            {
                var screenshotPath = Path.Combine(_imageDir, $"{_testName}_final_state.png");
                var suffix = 1;
                while (File.Exists(screenshotPath))
                {
                    screenshotPath = Path.Combine(_imageDir, $"{_testName}_final_state_{suffix}.png");
                    suffix++;
                }

                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });
                TestContext.AddTestAttachment(screenshotPath, "Final page screenshot");
                TestContext.Progress.WriteLine($"Screenshot saved as: {screenshotPath}");
            }
        }
        finally
        {
            if (Page != null)
            {
                await Page.CloseAsync();
            }

            if (_context != null)
            {
                await _context.CloseAsync();
            }

            if (_browser != null)
            {
                await _browser.DisposeAsync();
            }
        }

        string? sourceVideoPath = null;
        if (videoPathTask != null)
        {
            sourceVideoPath = await videoPathTask;
        }

        if (!string.IsNullOrWhiteSpace(sourceVideoPath) && File.Exists(sourceVideoPath))
        {
            var extension = Path.GetExtension(sourceVideoPath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".webm";
            }

            var targetVideoPath = Path.Combine(_videoDir, $"{_testArtifactPrefix}{extension}");
            var suffix = 1;
            while (File.Exists(targetVideoPath))
            {
                targetVideoPath = Path.Combine(_videoDir, $"{_testArtifactPrefix}_{suffix}{extension}");
                suffix++;
            }

            File.Move(sourceVideoPath, targetVideoPath);
            TestContext.AddTestAttachment(targetVideoPath, "Recorded video");
            TestContext.Progress.WriteLine($"Video saved as: {targetVideoPath}");
        }

        if (_setUpCompleted)
        {
            TestContext.Progress.WriteLine($"E2E slowMo: {TestSettings.PlaywrightSlowMoMs}ms");
            TestContext.Progress.WriteLine($"E2E headless: {TestSettings.PlaywrightHeadless}");
            TestContext.Progress.WriteLine($"E2E colorScheme: {TestSettings.PlaywrightColorScheme}");
        }
        TestContext.Progress.WriteLine($"Video files are saved in: {_videoDir}");
        TestContext.Progress.WriteLine($"Image files are saved in: {_imageDir}");
    }

    private static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }
}
