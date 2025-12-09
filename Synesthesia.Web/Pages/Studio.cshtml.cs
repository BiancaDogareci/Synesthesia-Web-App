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
            _env = env;
            _db = db;
        }

        [BindProperty]
        public string? AudioPath { get; set; }

        [BindProperty]
        public string? UploadedOriginalFileName { get; set; }

        [BindProperty]
        public string? Message { get; set; }

        public Guid? CurrentAudioId { get; set; }
        public bool IsCurrentAudioSaved { get; set; }
        public string? OriginalFileName => AudioPath != null ? System.IO.Path.GetFileName(AudioPath) : null;

        public async Task OnGetAsync(Guid? audioId)
        {
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
                Message = "Please select an audio file (.mp3 or .wav).";
                return Page();
            }

            var ext = Path.GetExtension(audioFile.FileName).ToLowerInvariant();
            if (ext != ".mp3" && ext != ".wav")
            {
                Message = "Only .mp3 and .wav files are allowed.";
                return Page();
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "audio");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

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
                Message = "Failed to save uploaded file: " + ex.Message;
                return Page();
            }

            // Only assign properties for UI rendering, DO NOT save to DB
            AudioPath = $"/uploads/audio/{newFileName}";
            UploadedOriginalFileName = audioFile.FileName; // store original filename
            CurrentAudioId = null; // No DB record yet
            IsCurrentAudioSaved = false;
            Message = "Audio uploaded. Click 'Save to Profile' to add to your history.";

            return Page();
        }

        public async Task<IActionResult> OnPostSaveToProfileAsync(string audioPath, string originalFileName)
        {
            if (!User?.Identity?.IsAuthenticated ?? true)
            {
                Message = "You must be logged in to save audio to your profile.";
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Check if the file already exists in DB
                var existing = await _db.AudioFiles
                    .FirstOrDefaultAsync(a => a.FilePath == audioPath);

                if (existing != null)
                {
                    if (existing.UserId != userId)
                    {
                        Message = "This audio was uploaded by another user.";
                        return Page();
                    }

                    // Already saved by this user
                    AudioPath = existing.FilePath;
                    CurrentAudioId = existing.Id;
                    IsCurrentAudioSaved = true;
                    Message = "Audio already saved to your profile.";
                    return Page();
                }

                // Create new record
                var audioRecord = new AudioFile
                {
                    UserId = userId,
                    FileName = originalFileName,
                    FilePath = audioPath,
                    Format = Path.GetExtension(audioPath).TrimStart('.')
                };

                _db.AudioFiles.Add(audioRecord);
                await _db.SaveChangesAsync();

                AudioPath = audioRecord.FilePath;
                CurrentAudioId = audioRecord.Id;
                IsCurrentAudioSaved = true;
                Message = "Audio saved to your profile successfully!";
            }
            catch (Exception ex)
            {
                Message = "Failed to save audio to profile: " + ex.Message;
            }

            return Page();
        }

    }
}