using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using Xunit;

namespace Synesthesia.Web.Tests;

public class StudioJavaScriptTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private string? _baseUrl;

    public StudioJavaScriptTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        _baseUrl = _factory.ServerAddress;
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
    }

    [Fact]
    public async Task StudioPage_LoadsSuccessfully()
    {
        var page = await _browser!.NewPageAsync();

        var response = await page.GotoAsync($"{_baseUrl}/Studio");

        Assert.NotNull(response);
        Assert.True(response.Ok);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_HexToRgb01Function_IsDefined()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");

        await page.WaitForFunctionAsync("typeof window.hexToRgb01 === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var isDefined = await page.EvaluateAsync<bool>(
            "typeof window.hexToRgb01 === 'function'"
        );

        Assert.True(isDefined);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_HexToRgb01_ConvertsColorsCorrectly()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.hexToRgb01 === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var red = await page.EvaluateAsync<double[]>("window.hexToRgb01('#FF0000')");
        Assert.Equal(1.0, red[0], precision: 2);
        Assert.Equal(0.0, red[1], precision: 2);
        Assert.Equal(0.0, red[2], precision: 2);

        var green = await page.EvaluateAsync<double[]>("window.hexToRgb01('#00FF00')");
        Assert.Equal(0.0, green[0], precision: 2);
        Assert.Equal(1.0, green[1], precision: 2);
        Assert.Equal(0.0, green[2], precision: 2);

        var blue = await page.EvaluateAsync<double[]>("window.hexToRgb01('#0000FF')");
        Assert.Equal(0.0, blue[0], precision: 2);
        Assert.Equal(0.0, blue[1], precision: 2);
        Assert.Equal(1.0, blue[2], precision: 2);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_JuliaPresets_AreAllDefined()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.juliaPresets === 'object'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var presets = await page.EvaluateAsync<string[]>(
            "Object.keys(window.juliaPresets)"
        );

        Assert.Contains("classic", presets);
        Assert.Contains("dragon", presets);
        Assert.Contains("snowflake", presets);
        Assert.Contains("spiral", presets);
        Assert.Contains("lotus", presets);
        Assert.Contains("chaos", presets);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_JuliaPresets_HaveCorrectValues()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.juliaPresets === 'object'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var classicCx = await page.EvaluateAsync<double>("window.juliaPresets.classic.cx");
        var classicCy = await page.EvaluateAsync<double>("window.juliaPresets.classic.cy");

        Assert.Equal(0.0, classicCx);
        Assert.Equal(0.8, classicCy);

        var chaosCx = await page.EvaluateAsync<double>("window.juliaPresets.chaos.cx");
        var chaosCy = await page.EvaluateAsync<double>("window.juliaPresets.chaos.cy");

        Assert.Equal(-0.4, chaosCx);
        Assert.Equal(-0.59, chaosCy);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_GetFractalType_ReturnsDefaultJulia()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.getFractalType === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var fractalType = await page.EvaluateAsync<string>("window.getFractalType()");

        Assert.Equal("julia", fractalType);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_GetFractalConfigSnapshot_ReturnsValidConfig()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.getFractalConfigSnapshot === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var fractalType = await page.EvaluateAsync<string>(
            "window.getFractalConfigSnapshot().fractalType"
        );
        Assert.Equal("julia", fractalType);

        var hasPrimary = await page.EvaluateAsync<bool>(
            "Array.isArray(window.getFractalConfigSnapshot().colors.primary)"
        );
        Assert.True(hasPrimary);

        var hasSecondary = await page.EvaluateAsync<bool>(
            "Array.isArray(window.getFractalConfigSnapshot().colors.secondary)"
        );
        Assert.True(hasSecondary);

        var bassStrength = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.bassStrength"
        );
        Assert.True(bassStrength > 0);

        var iterations = await page.EvaluateAsync<int>(
            "window.getFractalConfigSnapshot().quality.iterations"
        );
        Assert.True(iterations > 0);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesConfig()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var initialBass = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.bassStrength"
        );

        await page.EvaluateAsync(@"
            window.updateFractalConfig({ 
                motion: { bassStrength: 3.5 } 
            })
        ");

        var updatedBass = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.bassStrength"
        );

        Assert.NotEqual(initialBass, updatedBass);
        Assert.Equal(3.5, updatedBass, precision: 1);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioPage_FractalTypeDropdown_Exists()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");

        var dropdown = await page.QuerySelectorAsync("#fractalType");

        Assert.NotNull(dropdown);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioPage_ChangingFractalType_UpdatesConfig()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.getFractalType === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var initialType = await page.EvaluateAsync<string>("window.getFractalType()");
        Assert.Equal("julia", initialType);

        await page.SelectOptionAsync("#fractalType", "mandelbrot");
        await Task.Delay(500);

        var updatedType = await page.EvaluateAsync<string>(
            "window.getFractalConfigSnapshot().fractalType"
        );

        Assert.Equal("mandelbrot", updatedType);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioPage_FractalContainer_Exists()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");

        var container = await page.QuerySelectorAsync("#fractal-container");

        Assert.NotNull(container);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_InitVisualizer_CreatesCanvas()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var canvas = await page.QuerySelectorAsync("#fractal-container canvas");

        Assert.NotNull(canvas);

        await page.CloseAsync();
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string ServerAddress { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseKestrel();
        builder.UseUrls("http://127.0.0.1:0"); // Use random port
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHostBuilder => webHostBuilder.UseKestrel());

        var host = builder.Build();
        host.Start();

        // Get the server address
        var server = host.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        ServerAddress = addressFeature!.Addresses.First();

        return testHost;
    }

    public Task InitializeAsync()
    {
        _ = CreateClient();
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        return base.DisposeAsync().AsTask();
    }
}
