using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Synesthesia.Web.Data;
using Synesthesia.Web.Models;
using Synesthesia.Web.Pages;
using Xunit;

namespace Synesthesia.Web.Tests;

public class StudioModelTests
{
    private static (StudioModel model, ApplicationDbContext db, string webRoot) CreateModel(string dbName)
    {
        var db = TestHelpers.CreateInMemoryDb(dbName);

        var webRoot = Path.Combine(Path.GetTempPath(), "synesthesia-tests-wwwroot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRoot);

        var model = new StudioModel(env.Object, db);

        var (_, pc) = TestHelpers.BuildPageContext();
        model.PageContext = pc;

        return (model, db, webRoot);
    }

    [Fact]
    public async Task OnGetAsync_WithProjectId_LoadsProjectAndReturnsEarly()
    {
        var (model, db, _) = CreateModel(nameof(OnGetAsync_WithProjectId_LoadsProjectAndReturnsEarly));

        var audio = new AudioFile
        {
            UserId = "u1",
            FileName = "a.mp3",
            FilePath = "/uploads/audio/a.mp3",
            Format = "mp3"
        };
        db.AudioFiles.Add(audio);
        await db.SaveChangesAsync();

        var project = new FractalProject
        {
            UserId = "u1",
            AudioId = audio.Id,
            Title = "p",
            FractalType = "mandelbrot",
            SettingsJson = "{\"fractalType\":\"mandelbrot\"}"
        };
        db.FractalProjects.Add(project);
        await db.SaveChangesAsync();

        await model.OnGetAsync(audioId: null, projectId: project.Id);

        Assert.Equal("/uploads/audio/a.mp3", model.AudioPath);
        Assert.Equal("mandelbrot", model.ProjectFractalType);
        Assert.Equal(project.SettingsJson, model.ProjectSettingsJson);
    }

    [Fact]
    public async Task OnGetAsync_WithAudioId_LoadsAudio()
    {
        var (model, db, _) = CreateModel(nameof(OnGetAsync_WithAudioId_LoadsAudio));

        var audio = new AudioFile
        {
            UserId = "u1",
            FileName = "song.wav",
            FilePath = "/uploads/audio/x.wav",
            Format = "wav"
        };
        db.AudioFiles.Add(audio);
        await db.SaveChangesAsync();

        await model.OnGetAsync(audioId: audio.Id, projectId: null);

        Assert.Equal("/uploads/audio/x.wav", model.AudioPath);
        Assert.Equal(audio.Id, model.CurrentAudioId);
        Assert.True(model.IsCurrentAudioSaved);
    }

    [Fact]
    public async Task OnPostUploadAsync_WhenNull_ReturnsErrorJson()
    {
        var (model, _, _) = CreateModel(nameof(OnPostUploadAsync_WhenNull_ReturnsErrorJson));

        var result = await model.OnPostUploadAsync(audioFile: null!);
        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.False(TestHelpers.GetProp<bool>(json.Value!, "success"));
        Assert.NotNull(TestHelpers.GetProp<string>(json.Value!, "message"));
    }

    [Fact]
    public async Task OnPostUploadAsync_WhenInvalidExt_ReturnsErrorJson()
    {
        var (model, _, _) = CreateModel(nameof(OnPostUploadAsync_WhenInvalidExt_ReturnsErrorJson));

        var file = CreateFormFile("track.ogg", "audio/ogg", "dummy");
        var result = await model.OnPostUploadAsync(file);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.False(TestHelpers.GetProp<bool>(json.Value!, "success"));
        Assert.NotNull(TestHelpers.GetProp<string>(json.Value!, "message"));
    }

    [Fact]
    public async Task OnPostUploadAsync_WhenValidMp3_SavesFile_AndReturnsSuccess()
    {
        var (model, _, webRoot) = CreateModel(nameof(OnPostUploadAsync_WhenValidMp3_SavesFile_AndReturnsSuccess));

        var file = CreateFormFile("track.mp3", "audio/mpeg", "abc123");
        var result = await model.OnPostUploadAsync(file);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.True(TestHelpers.GetProp<bool>(json.Value!, "success"));

        var audioPath = TestHelpers.GetProp<string>(json.Value!, "audioPath");
        Assert.StartsWith("/uploads/audio/", audioPath);

        var physical = Path.Combine(webRoot, audioPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physical));
    }

    [Fact]
    public async Task OnPostSaveToProfileAsync_WhenNotAuthenticated_ReturnsError()
    {
        var (model, _, _) = CreateModel(nameof(OnPostSaveToProfileAsync_WhenNotAuthenticated_ReturnsError));

        var (_, pc) = TestHelpers.BuildPageContext(TestHelpers.BuildUser("u1", authenticated: false));
        model.PageContext = pc;

        var result = await model.OnPostSaveToProfileAsync(
            audioPath: "/uploads/audio/a.mp3",
            originalFileName: "a.mp3",
            fractalType: "julia",
            settingsJson: "{}",
            title: null
        );

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.False(TestHelpers.GetProp<bool>(json.Value!, "success"));
        Assert.NotNull(TestHelpers.GetProp<string>(json.Value!, "message"));
    }

    [Fact]
    public async Task OnPostSaveToProfileAsync_WhenMissingAudio_ReturnsError()
    {
        var (model, _, _) = CreateModel(nameof(OnPostSaveToProfileAsync_WhenMissingAudio_ReturnsError));

        var (_, pc) = TestHelpers.BuildPageContext(TestHelpers.BuildUser("u1"));
        model.PageContext = pc;

        var result = await model.OnPostSaveToProfileAsync(
            audioPath: "",
            originalFileName: "",
            fractalType: "julia",
            settingsJson: "{}",
            title: null
        );

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.False(TestHelpers.GetProp<bool>(json.Value!, "success"));
        Assert.NotNull(TestHelpers.GetProp<string>(json.Value!, "message"));
    }

    [Fact]
    public async Task OnPostSaveToProfileAsync_WhenMissingSettings_ReturnsError()
    {
        var (model, _, _) = CreateModel(nameof(OnPostSaveToProfileAsync_WhenMissingSettings_ReturnsError));

        var (_, pc) = TestHelpers.BuildPageContext(TestHelpers.BuildUser("u1"));
        model.PageContext = pc;

        var result = await model.OnPostSaveToProfileAsync(
            audioPath: "/uploads/audio/a.mp3",
            originalFileName: "a.mp3",
            fractalType: "",
            settingsJson: "",
            title: null
        );

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.False(TestHelpers.GetProp<bool>(json.Value!, "success"));
        Assert.NotNull(TestHelpers.GetProp<string>(json.Value!, "message"));
    }

    [Fact]
    public async Task OnPostSaveToProfileAsync_CreatesAudioIfMissing_ThenCreatesProject()
    {
        var (model, db, _) = CreateModel(nameof(OnPostSaveToProfileAsync_CreatesAudioIfMissing_ThenCreatesProject));

        var (_, pc) = TestHelpers.BuildPageContext(TestHelpers.BuildUser("u1"));
        model.PageContext = pc;

        var audioPath = "/uploads/audio/new.mp3";

        var result = await model.OnPostSaveToProfileAsync(
            audioPath: audioPath,
            originalFileName: "original.mp3",
            fractalType: "mandelbrot",
            settingsJson: "{\"fractalType\":\"mandelbrot\"}",
            title: null
        );

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);

        Assert.True(TestHelpers.GetProp<bool>(json.Value!, "success"));

        var audio = await db.AudioFiles.FirstOrDefaultAsync(a => a.FilePath == audioPath);
        Assert.NotNull(audio);
        Assert.Equal("u1", audio!.UserId);
        Assert.Equal("original.mp3", audio.FileName);

        var project = await db.FractalProjects.FirstOrDefaultAsync(p => p.AudioId == audio.Id);
        Assert.NotNull(project);
        Assert.Equal("mandelbrot", project!.FractalType);
        Assert.Contains("mandelbrot", project.Title);
    }

    [Fact]
    public async Task OnPostSaveToProfileAsync_ReusesExistingAudio_CreatesProject()
    {
        var (model, db, _) = CreateModel(nameof(OnPostSaveToProfileAsync_ReusesExistingAudio_CreatesProject));

        var existing = new AudioFile
        {
            UserId = "u1",
            FileName = "song.mp3",
            FilePath = "/uploads/audio/existing.mp3",
            Format = "mp3"
        };
        db.AudioFiles.Add(existing);
        await db.SaveChangesAsync();

        var (_, pc) = TestHelpers.BuildPageContext(TestHelpers.BuildUser("u1"));
        model.PageContext = pc;

        var result = await model.OnPostSaveToProfileAsync(
            audioPath: existing.FilePath,
            originalFileName: "ignored.mp3",
            fractalType: "julia",
            settingsJson: "{}",
            title: "My Title"
        );

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(result);
        Assert.True(TestHelpers.GetProp<bool>(json.Value!, "success"));

        var audiosCount = await db.AudioFiles.CountAsync();
        Assert.Equal(1, audiosCount);

        var project = await db.FractalProjects.FirstOrDefaultAsync();
        Assert.NotNull(project);
        Assert.Equal(existing.Id, project!.AudioId);
        Assert.Equal("My Title", project.Title);
    }

    private static IFormFile CreateFormFile(string fileName, string contentType, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, bytes.Length, "audioFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
