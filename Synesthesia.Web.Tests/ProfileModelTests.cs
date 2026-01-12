using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Synesthesia.Web.Data;
using Synesthesia.Web.Models;
using Synesthesia.Web.Pages;
using Xunit;

namespace Synesthesia.Web.Tests;

public class ProfileModelTests
{
    private static Mock<UserManager<AppUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<AppUser>>();
        return new Mock<UserManager<AppUser>>(
            store.Object,
            null!, null!, null!, null!, null!, null!, null!, null!
        );
    }

    private static (ProfileModel model, ApplicationDbContext db, string webRoot) CreateModel(string dbName, AppUser? user, bool authenticated)
    {
        var db = TestHelpers.CreateInMemoryDb(dbName);

        var webRoot = Path.Combine(Path.GetTempPath(), "synesthesia-tests-wwwroot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(webRoot);

        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(webRoot);

        var userMgr = MockUserManager();
        userMgr.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
               .ReturnsAsync(user);

        var logger = new Mock<ILogger<ProfileModel>>();

        var model = new ProfileModel(userMgr.Object, db, env.Object);

        var principal = authenticated
            ? TestHelpers.BuildUser(user?.Id ?? "u1", user?.UserName ?? "u1", authenticated: true)
            : TestHelpers.BuildUser("u1", authenticated: false);

        var (http, pc) = TestHelpers.BuildPageContext(principal);
        model.PageContext = pc;
        model.TempData = TestHelpers.BuildTempData(http);

        return (model, db, webRoot);
    }

    [Fact]
    public async Task OnGetAsync_WhenUserNull_AddsDebugInfoAndReturns()
    {
        var (model, _, _) = CreateModel(nameof(OnGetAsync_WhenUserNull_AddsDebugInfoAndReturns), user: null, authenticated: false);

        await model.OnGetAsync();

        Assert.NotNull(model.DebugInfo);
        Assert.Contains(model.DebugInfo!, s => s.Contains("User is NULL"));
    }

    [Fact]
    public async Task OnGetAsync_LoadsAudioAndProjects_ForUser()
    {
        var user = new AppUser { Id = "u1", UserName = "alice", Bio = "bio", ProfilePicture = "/p.png" };
        var (model, db, _) = CreateModel(nameof(OnGetAsync_LoadsAudioAndProjects_ForUser), user, authenticated: true);

        var audio1 = new AudioFile { UserId = "u1", FileName = "a.mp3", FilePath = "/uploads/audio/a.mp3", Format = "mp3" };
        var audioOther = new AudioFile { UserId = "u2", FileName = "b.mp3", FilePath = "/uploads/audio/b.mp3", Format = "mp3" };
        db.AudioFiles.AddRange(audio1, audioOther);
        await db.SaveChangesAsync();

        var proj = new FractalProject
        {
            UserId = "u1",
            AudioId = audio1.Id,
            Title = "t",
            FractalType = "julia",
            SettingsJson = "{}"
        };
        db.FractalProjects.Add(proj);
        await db.SaveChangesAsync();

        await model.OnGetAsync();

        Assert.Equal("alice", model.Username);
        Assert.Equal("/p.png", model.ProfilePicture);
        Assert.Equal("bio", model.Bio);

        Assert.Single(model.AudioFiles);
        Assert.Single(model.Projects);
        Assert.Contains(model.DebugInfo!, s => s.Contains("Projects for this user: 1"));
    }

    [Fact]
    public async Task OnPostDeleteProjectAsync_WhenNotAuthenticated_RedirectsToLogin()
    {
        var user = new AppUser { Id = "u1", UserName = "alice" };
        var (model, _, _) = CreateModel(nameof(OnPostDeleteProjectAsync_WhenNotAuthenticated_RedirectsToLogin), user, authenticated: false);

        var result = await model.OnPostDeleteProjectAsync(Guid.NewGuid());

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Account/Login", redirect.PageName);
        Assert.Equal("Identity", redirect.RouteValues!["area"]);
    }

    [Fact]
    public async Task OnPostDeleteProjectAsync_WhenProjectNotFound_SetsTempDataError()
    {
        var user = new AppUser { Id = "u1", UserName = "alice" };
        var (model, _, _) = CreateModel(nameof(OnPostDeleteProjectAsync_WhenProjectNotFound_SetsTempDataError), user, authenticated: true);

        var result = await model.OnPostDeleteProjectAsync(Guid.NewGuid());

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Project not found or you don't have permission to delete it.", model.TempData["Error"]);
    }

    [Fact]
    public async Task OnPostDeleteProjectAsync_WhenOtherProjectsUseAudio_DeletesProjectKeepsAudio()
    {
        var user = new AppUser { Id = "u1", UserName = "alice" };
        var (model, db, webRoot) = CreateModel(nameof(OnPostDeleteProjectAsync_WhenOtherProjectsUseAudio_DeletesProjectKeepsAudio), user, authenticated: true);

        // create physical file
        var relPath = "/uploads/audio/shared.mp3";
        var physical = Path.Combine(webRoot, relPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        await File.WriteAllTextAsync(physical, "x");

        var audio = new AudioFile { UserId = "u1", FileName = "shared.mp3", FilePath = relPath, Format = "mp3" };
        db.AudioFiles.Add(audio);
        await db.SaveChangesAsync();

        var p1 = new FractalProject { UserId = "u1", AudioId = audio.Id, Title = "p1", FractalType = "julia", SettingsJson = "{}" };
        var p2 = new FractalProject { UserId = "u1", AudioId = audio.Id, Title = "p2", FractalType = "mandelbrot", SettingsJson = "{}" };
        db.FractalProjects.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var result = await model.OnPostDeleteProjectAsync(p1.Id);

        Assert.IsType<RedirectToPageResult>(result);

        // p1 deleted, p2 remains
        Assert.False(await db.FractalProjects.AnyAsync(p => p.Id == p1.Id));
        Assert.True(await db.FractalProjects.AnyAsync(p => p.Id == p2.Id));

        // audio kept + file kept
        Assert.True(await db.AudioFiles.AnyAsync(a => a.Id == audio.Id));
        Assert.True(File.Exists(physical));

        Assert.Equal("Project deleted. Audio kept (used by other projects).", model.TempData["Success"]);
    }

    [Fact]
    public async Task OnPostDeleteProjectAsync_WhenNoOtherProjectsUseAudio_DeletesProjectAudioAndPhysicalFile()
    {
        var user = new AppUser { Id = "u1", UserName = "alice" };
        var (model, db, webRoot) = CreateModel(nameof(OnPostDeleteProjectAsync_WhenNoOtherProjectsUseAudio_DeletesProjectAudioAndPhysicalFile), user, authenticated: true);

        // create physical file
        var relPath = "/uploads/audio/alone.mp3";
        var physical = Path.Combine(webRoot, relPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
        await File.WriteAllTextAsync(physical, "x");

        var audio = new AudioFile { UserId = "u1", FileName = "alone.mp3", FilePath = relPath, Format = "mp3" };
        db.AudioFiles.Add(audio);
        await db.SaveChangesAsync();

        var project = new FractalProject { UserId = "u1", AudioId = audio.Id, Title = "p", FractalType = "julia", SettingsJson = "{}" };
        db.FractalProjects.Add(project);
        await db.SaveChangesAsync();

        var result = await model.OnPostDeleteProjectAsync(project.Id);

        Assert.IsType<RedirectToPageResult>(result);

        Assert.False(await db.FractalProjects.AnyAsync(p => p.Id == project.Id));
        Assert.False(await db.AudioFiles.AnyAsync(a => a.Id == audio.Id));
        Assert.False(File.Exists(physical));

        Assert.Equal("Project and audio deleted successfully.", model.TempData["Success"]);
    }
}
