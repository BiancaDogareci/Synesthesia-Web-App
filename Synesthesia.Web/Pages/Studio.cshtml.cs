using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Synesthesia.Web.Data;
using Synesthesia.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Synesthesia.Web.Pages
{
    public class StudioModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _db;

        public StudioModel(IWebHostEnvironment env, ApplicationDbContext db)
        {
            _env = env; // _env gives access to server paths (needed to save uploaded files to wwwroot/uploads/audio)
            _db = db; // _db is the Entity Framework database context
        }



        // PROPERTIES
        [BindProperty] // binds Razor form inputs to a specific property
        public string? AudioPath { get; set; }

        [BindProperty]
        public string? UploadedOriginalFileName { get; set; } // user’s original file name

        [BindProperty]
        public string? Message { get; set; } // stores feedback messages

        public Guid? CurrentAudioId { get; set; }
        public bool IsCurrentAudioSaved { get; set; }
        public string? OriginalFileName => AudioPath != null ? System.IO.Path.GetFileName(AudioPath) : null; // filename from AudioPath (unique gibblerish)
        public string? ProjectSettingsJson { get; set; }
        public string? ProjectFractalType { get; set; }

        // Runs when the page is first loaded
        public async Task OnGetAsync(Guid? audioId, Guid? projectId)
        {
            if (projectId.HasValue)
            {
                var project = await _db.FractalProjects
                    .Include(p => p.AudioFile)
                    .FirstOrDefaultAsync(p => p.Id == projectId.Value);

                if (project != null)
                {
                    AudioPath = project.AudioFile?.FilePath;
                    ProjectSettingsJson = project.SettingsJson;
                    ProjectFractalType = project.FractalType;
                }
                return;
            }

            if (audioId.HasValue)
            {
                var audioFile = await _db.AudioFiles
                    .FirstOrDefaultAsync(a => a.Id == audioId.Value);

                if (audioFile != null)
                {
                    AudioPath = audioFile.FilePath;
                    CurrentAudioId = audioFile.Id;
                    IsCurrentAudioSaved = !string.IsNullOrEmpty(audioFile.UserId);
                }
            }
        }

        public async Task<IActionResult> OnPostUploadAsync(IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return new JsonResult(new { success = false, message = "Please select an audio file (.mp3 or .wav)." });
            }

            var ext = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".wav")
            {
                return new JsonResult(new { success = false, message = "Only .mp3 and .wav files are allowed." });
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "audio");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var newFileName = Guid.NewGuid().ToString("N") + ext;
            var filePath = Path.Combine(uploadsFolder, newFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await audioFile.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Failed to save uploaded file: " + ex.Message });
            }

            var audioPath = $"/uploads/audio/{newFileName}";

            return new JsonResult(new { success = true, audioPath, originalFileName = audioFile.FileName });
        }

        public async Task<IActionResult> OnPostSaveToProfileAsync(
            string audioPath,
            string originalFileName,
            string fractalType,
            string settingsJson,
            string? title
        )
        {
            if (!User?.Identity?.IsAuthenticated ?? true)
                return new JsonResult(new { success = false, message = "You must be logged in." });

            if (string.IsNullOrWhiteSpace(audioPath) || string.IsNullOrWhiteSpace(originalFileName))
                return new JsonResult(new { success = false, message = "Please upload an audio file first." });

            if (string.IsNullOrWhiteSpace(fractalType) || string.IsNullOrWhiteSpace(settingsJson))
                return new JsonResult(new { success = false, message = "Missing fractal settings." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // 1) Ensure AudioFile exists
                var audio = await _db.AudioFiles.FirstOrDefaultAsync(a => a.FilePath == audioPath);

                if (audio == null)
                {
                    var ext = Path.GetExtension(originalFileName);
                    var format = string.IsNullOrWhiteSpace(ext) ? "unknown" : ext.TrimStart('.').ToLowerInvariant();

                    audio = new AudioFile
                    {
                        UserId = userId,
                        FileName = originalFileName,
                        FilePath = audioPath,
                        Format = format
                    };

                    _db.AudioFiles.Add(audio);
                    await _db.SaveChangesAsync();
                }

                // 2) Save a "project" snapshot (this is the real value)
                var project = new FractalProject
                {
                    UserId = userId,
                    AudioId = audio.Id,
                    Title = string.IsNullOrWhiteSpace(title) ? $"{fractalType} - {DateTime.Now:yyyy-MM-dd HH:mm}" : title,
                    FractalType = fractalType,
                    SettingsJson = settingsJson
                };

                _db.FractalProjects.Add(project);
                await _db.SaveChangesAsync();

                return new JsonResult(new { success = true, projectId = project.Id });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = "Failed to save: " + ex.Message });
            }
        }

    }
}