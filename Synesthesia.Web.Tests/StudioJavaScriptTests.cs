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

    //[Fact]
    //public async Task StudioJS_SavedToPartialConfig_Julia_IncludesPreset()
    //{
    //    var page = await _browser!.NewPageAsync();

    //    await page.GotoAsync($"{_baseUrl}/Studio");
    //    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    //    // Test that savedToPartialConfig properly converts julia preset
    //    var result = await page.EvaluateAsync<bool>(@"
    //    (() => {
    //    const saved = {
    //        fractalType: 'julia',
    //        juliaPreset: 'dragon',
    //        iterations: 250,
    //        bassStrength: 2.0,
    //        primaryColor: '#FF0000',
    //        secondaryColor: '#00FF00',
    //        rainbow: true
    //    };

    //    await page.EvaluateAsync(@""
    //    window.juliaPresets = {
    //        classic: { cx: 0.0, cy: 0.8 },
    //        dragon: { cx: 0.37, cy: 0.1 },
    //        snowflake: { cx: 0.355, cy: 0.355 },
    //        spiral: { cx: 0.34, cy: -0.05 },
    //        lotus: { cx: -0.54, cy: 0.54 },
    //        chaos: { cx: -0.4, cy: -0.59 }
    //    };
    //    "");
        
    //    // This function should exist from fractal-studio.js
    //    const partial = window.savedToPartialConfig ? 
    //        window.savedToPartialConfig(saved) : null;
        
    //    if (!partial) return false;
        
    //    return partial.fractalType === 'julia' &&
    //           partial.julia && 
    //           partial.julia.cx === 0.37 &&
    //           partial.julia.cy === 0.1 &&
    //           partial.quality.iterations === 250 &&
    //           partial.motion.bassStrength === 2.0 &&
    //           partial.colorMode.rainbow === true;
    //    })()
    //");

    //    Assert.True(result, "savedToPartialConfig should properly convert saved settings");

    //    await page.CloseAsync();
    //}

    //[Fact]
    //public async Task StudioJS_SavedToPartialConfig_Mandelbulb_IncludesQuality()
    //{
    //    var page = await _browser!.NewPageAsync();

    //    await page.GotoAsync($"{_baseUrl}/Studio");
    //    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    //    var hasCorrectValues = await page.EvaluateAsync<bool>(@"
    //    (() => {
    //    const saved = {
    //        fractalType: 'mandelbulb',
    //        raySteps: 150,
    //        rotationSpeed: 1.5,
    //        zoomPulse: 0.3,
    //        bassStrength: 3.0
    //    };
        
    //    const partial = window.savedToPartialConfig ?
    //        window.savedToPartialConfig(saved) : null;
        
    //    if (!partial) return false;
        
    //    return partial.fractalType === 'mandelbulb' &&
    //           partial.quality.raySteps === 150 &&
    //           partial.motion.rotationSpeed === 1.5 &&
    //           partial.motion.zoomPulse === 0.3 &&
    //           partial.motion.bassStrength === 3.0;
    //    })()
    //");

    //    Assert.True(hasCorrectValues);

    //    await page.CloseAsync();
    //}

    [Fact]
    public async Task StudioJS_DefaultConfig_HasCorrectJuliaValues()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.getFractalConfigSnapshot === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Check default Julia c values
        var juliaCx = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().julia.cx"
        );
        var juliaCy = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().julia.cy"
        );

        Assert.Equal(-0.4, juliaCx, precision: 2);
        Assert.Equal(-0.59, juliaCy, precision: 2);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesJuliaPreset()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Update to use the "classic" Julia preset
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            julia: window.juliaPresets.classic
        })
    ");

        var cx = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().julia.cx"
        );
        var cy = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().julia.cy"
        );

        Assert.Equal(0.0, cx);
        Assert.Equal(0.8, cy);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesColorMode()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Enable rainbow mode
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            colorMode: { rainbow: true }
        })
    ");

        var rainbowEnabled = await page.EvaluateAsync<bool>(
            "window.getFractalConfigSnapshot().colorMode.rainbow"
        );

        Assert.True(rainbowEnabled);

        // Disable rainbow mode
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            colorMode: { rainbow: false }
        })
    ");

        var rainbowDisabled = await page.EvaluateAsync<bool>(
            "window.getFractalConfigSnapshot().colorMode.rainbow"
        );

        Assert.False(rainbowDisabled);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesColors()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Update primary color
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            colors: { primary: [1.0, 0.0, 0.0] }
        })
    ");

        var primaryRed = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().colors.primary[0]"
        );

        Assert.Equal(1.0, primaryRed, precision: 2);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesQualitySettings()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Update iterations
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            quality: { iterations: 500 }
        })
    ");

        var iterations = await page.EvaluateAsync<int>(
            "window.getFractalConfigSnapshot().quality.iterations"
        );

        Assert.Equal(500, iterations);

        // Update ray steps
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            quality: { raySteps: 150 }
        })
    ");

        var raySteps = await page.EvaluateAsync<int>(
            "window.getFractalConfigSnapshot().quality.raySteps"
        );

        Assert.Equal(150, raySteps);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioPage_FractalDropdown_HasAllOptions()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");

        var options = await page.EvaluateAsync<string[]>(@"
        (() => {
        const select = document.getElementById('fractalType');
        return Array.from(select.options).map(opt => opt.value);
        })()
    ");

        Assert.Contains("julia", options);
        Assert.Contains("mandelbrot", options);
        Assert.Contains("mandelbulb", options);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_InitVisualizer_ReturnsCleanupFunction()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var hasCleanup = await page.EvaluateAsync<bool>(@"
        (() => {
            const cleanup = window.initVisualizer('julia', 'fractal-container');
            return typeof cleanup === 'function';
        })()
        ");

        Assert.True(hasCleanup, "initVisualizer should return a cleanup function");

        await page.CloseAsync();
    }

    //[Fact]
    //public async Task StudioJS_InitVisualizer_CreatesDifferentShadersForDifferentFractals()
    //{
    //    var page = await _browser!.NewPageAsync();

    //    await page.GotoAsync($"{_baseUrl}/Studio");
    //    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

    //    // Initialize with Julia
    //    await page.EvaluateAsync("window.initVisualizer('julia', 'fractal-container')");
    //    await page.WaitForSelectorAsync("#fractal-container canvas", new() { State = WaitForSelectorState.Visible });

    //    var juliaCanvas = await page.QuerySelectorAsync("#fractal-container canvas");
    //    Assert.NotNull(juliaCanvas);

    //    // Switch to Mandelbrot
    //    await page.EvaluateAsync("window.initVisualizer('julia', 'fractal-container')");
    //    await page.WaitForSelectorAsync("#fractal-container canvas", new() { State = WaitForSelectorState.Visible });


    //    var mandelbrotCanvas = await page.QuerySelectorAsync("#fractal-container canvas");
    //    Assert.NotNull(mandelbrotCanvas);

    //    // Switch to Mandelbulb
    //    await page.EvaluateAsync("window.initVisualizer('julia', 'fractal-container')");
    //    await page.WaitForSelectorAsync("#fractal-container canvas", new() { State = WaitForSelectorState.Visible });

    //    var mandelbulbCanvas = await page.QuerySelectorAsync("#fractal-container canvas");
    //    Assert.NotNull(mandelbulbCanvas);

    //    await page.CloseAsync();
    //}

    [Fact]
    public async Task StudioJS_HexToRgb01_HandlesVariousColors()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.hexToRgb01 === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Test white
        var white = await page.EvaluateAsync<double[]>("window.hexToRgb01('#FFFFFF')");
        Assert.Equal(1.0, white[0], precision: 2);
        Assert.Equal(1.0, white[1], precision: 2);
        Assert.Equal(1.0, white[2], precision: 2);

        // Test black
        var black = await page.EvaluateAsync<double[]>("window.hexToRgb01('#000000')");
        Assert.Equal(0.0, black[0], precision: 2);
        Assert.Equal(0.0, black[1], precision: 2);
        Assert.Equal(0.0, black[2], precision: 2);

        // Test purple (mix)
        var purple = await page.EvaluateAsync<double[]>("window.hexToRgb01('#800080')");
        Assert.True(purple[0] > 0.4 && purple[0] < 0.6); // 0.5
        Assert.Equal(0.0, purple[1], precision: 2);
        Assert.True(purple[2] > 0.4 && purple[2] < 0.6); // 0.5

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioPage_AudioElement_Exists()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");

        var audio = await page.QuerySelectorAsync("#audio");

        Assert.NotNull(audio);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_UpdateFractalConfig_UpdatesMotionSettings()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.updateFractalConfig === 'function'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        // Update rotation speed
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            motion: { rotationSpeed: 1.5 }
        })
    ");

        var rotationSpeed = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.rotationSpeed"
        );

        Assert.Equal(1.5, rotationSpeed, precision: 1);

        // Update zoom pulse
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            motion: { zoomPulse: 0.2 }
        })
    ");

        var zoomPulse = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.zoomPulse"
        );

        Assert.Equal(0.2, zoomPulse, precision: 2);

        // Update treble strength
        await page.EvaluateAsync(@"
        window.updateFractalConfig({
            motion: { trebleStrength: 2.5 }
        })
    ");

        var trebleStrength = await page.EvaluateAsync<double>(
            "window.getFractalConfigSnapshot().motion.trebleStrength"
        );

        Assert.Equal(2.5, trebleStrength, precision: 1);

        await page.CloseAsync();
    }

    [Fact]
    public async Task StudioJS_AllJuliaPresets_HaveValidCoordinates()
    {
        var page = await _browser!.NewPageAsync();

        await page.GotoAsync($"{_baseUrl}/Studio");
        await page.WaitForFunctionAsync("typeof window.juliaPresets === 'object'",
            new PageWaitForFunctionOptions { Timeout = 5000 });

        var allPresetsValid = await page.EvaluateAsync<bool>(@"
        (() => {
        const presets = window.juliaPresets;
        const keys = ['classic', 'dragon', 'snowflake', 'spiral', 'lotus', 'chaos'];
        
        for (const key of keys) {
            if (!presets[key]) return false;
            if (typeof presets[key].cx !== 'number') return false;
            if (typeof presets[key].cy !== 'number') return false;
            // Julia set coordinates are typically in range [-2, 2]
            if (Math.abs(presets[key].cx) > 2) return false;
            if (Math.abs(presets[key].cy) > 2) return false;
        }
        
        return true;
        })()
    ");

        Assert.True(allPresetsValid, "All Julia presets should have valid cx and cy coordinates");

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
