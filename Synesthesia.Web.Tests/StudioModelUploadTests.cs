using System;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Moq;
using Synesthesia.Web.Data;
using Synesthesia.Web.Models;
using Synesthesia.Web.Pages;
using Xunit;

namespace Synesthesia.Web.Tests
{
    public class StudioModelUploadTests
    {
        private ApplicationDbContext GetInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        private Mock<IWebHostEnvironment> MockEnv(string webRoot)
        {
            var m = new Mock<IWebHostEnvironment>();
            m.Setup(e => e.WebRootPath).Returns(webRoot);
            return m;
        }

        private IFormFile CreateFormFile(string fileName, string content, string contentType = "audio/mpeg")
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            var formFile = new FormFile(stream, 0, bytes.Length, "audioFile", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            return formFile;
        }

        private ClaimsPrincipal AuthenticatedPrincipal(string userId)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        private PageContext MakePageContextWithUser(ClaimsPrincipal user)
        {
            var ctx = new DefaultHttpContext();
            ctx.User = user ?? new ClaimsPrincipal();
            return new PageContext { HttpContext = ctx };
        }

        [Fact]
        public async Task OnPostUploadAsync_ValidMp3_SavesFileAndDbRecord()
        {
            var db = GetInMemoryDb();
            var temp = Path.Combine(Path.GetTempPath(), "syn-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);

            var envMock = MockEnv(temp);

            var model = new StudioModel(envMock.Object, db);
            model.PageContext = MakePageContextWithUser(AuthenticatedPrincipal("user-42"));

            var formFile = CreateFormFile("song.mp3", "FAKE_MP3_BYTES");

            var result = await model.OnPostUploadAsync(formFile);

            // DB contains one record
            Assert.Equal(1, await db.AudioFiles.CountAsync());

            var saved = await db.AudioFiles.FirstAsync();
            Assert.Equal("song.mp3", saved.FileName);
            Assert.Equal("mp3", saved.Format);
            Assert.Equal("user-42", saved.UserId);
            Assert.False(string.IsNullOrEmpty(model.AudioPath)); // path exposed to view

            // file physically exists on disk
            var expectedFull = Path.Combine(temp, "uploads", "audio", Path.GetFileName(saved.FilePath));
            Assert.True(File.Exists(expectedFull));

            try { Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task OnPostUploadAsync_AnonymousUser_SavesRecordWithEmptyUserId()
        {
            var db = GetInMemoryDb();
            var temp = Path.Combine(Path.GetTempPath(), "syn-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);

            var envMock = MockEnv(temp);
            var model = new StudioModel(envMock.Object, db);
            model.PageContext = MakePageContextWithUser(new ClaimsPrincipal());

            var formFile = CreateFormFile("ambient.wav", "FAKE_WAV_BYTES", contentType: "audio/wav");

            await model.OnPostUploadAsync(formFile);

            var saved = await db.AudioFiles.FirstAsync();
            Assert.Equal("wav", saved.Format);
            Assert.True(string.IsNullOrEmpty(saved.UserId) || saved.UserId == "");
            try { Directory.Delete(temp, true); } catch { }
        }

        [Fact]
        public async Task OnPostUploadAsync_InvalidExtension_ReturnsPageAndNoDbEntry()
        {
            var db = GetInMemoryDb();
            var envMock = MockEnv(Path.GetTempPath());
            var model = new StudioModel(envMock.Object, db);
            model.PageContext = MakePageContextWithUser(AuthenticatedPrincipal("u"));

            var formFile = CreateFormFile("bad.txt", "NOT_AUDIO", contentType: "text/plain");

            var pageResult = await model.OnPostUploadAsync(formFile);

            Assert.NotNull(model.Message);
            Assert.Contains(".mp3", model.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await db.AudioFiles.CountAsync());
        }

        [Fact]
        public async Task OnPostUploadAsync_NullFile_ReturnsPageAndNoDbEntry()
        {
            var db = GetInMemoryDb();
            var envMock = MockEnv(Path.GetTempPath());
            var model = new StudioModel(envMock.Object, db);
            model.PageContext = MakePageContextWithUser(AuthenticatedPrincipal("u"));

            var result = await model.OnPostUploadAsync(null);

            Assert.NotNull(model.Message);
            Assert.Contains("Please select", model.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await db.AudioFiles.CountAsync());
        }

        [Fact]
        public async Task OnPostUploadAsync_FileCopyThrows_ReturnsPageAndNoDbEntry()
        {
            var db = GetInMemoryDb();
            var envMock = MockEnv(Path.GetTempPath());
            var model = new StudioModel(envMock.Object, db);
            model.PageContext = MakePageContextWithUser(AuthenticatedPrincipal("u"));

            // Create a mocked IFormFile which throws when CopyToAsync is called
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.Length).Returns(10L);
            mockFile.Setup(f => f.FileName).Returns("fail.mp3");
            mockFile.Setup(f => f.ContentType).Returns("audio/mpeg");
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new IOException("disk write error"));

            var result = await model.OnPostUploadAsync(mockFile.Object);

            Assert.NotNull(model.Message);
            Assert.Contains("Failed to save uploaded file", model.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await db.AudioFiles.CountAsync());
        }

        // helper: a DbContext subclass that simulates failure on SaveChangesAsync
        private class FailingDbContext : ApplicationDbContext
        {
            public FailingDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

            public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                throw new Exception("Simulated DB failure");
            }
        }

        [Fact]
        public async Task OnPostUploadAsync_DbSaveThrows_FallsBackAndSetsAudioPathMessage()
        {
            // Arrange: create a real in-memory options, but use failing context wrapper
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"FailDb_{Guid.NewGuid()}")
                .Options;

            using var failing = new FailingDbContext(options);

            var temp = Path.Combine(Path.GetTempPath(), "syn-tests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp);

            var envMock = MockEnv(temp);

            var model = new StudioModel(envMock.Object, failing);
            model.PageContext = MakePageContextWithUser(AuthenticatedPrincipal("someone"));

            var formFile = CreateFormFile("song.mp3", "FAKE");

            var result = await model.OnPostUploadAsync(formFile);

            // Assert: when DB save fails, code sets AudioPath fallback and sets Message
            Assert.False(string.IsNullOrEmpty(model.AudioPath));
            Assert.NotNull(model.Message);
            Assert.Contains("failed to save record", model.Message, StringComparison.OrdinalIgnoreCase);

            try { Directory.Delete(temp, true); } catch { }
        }
    }
}
