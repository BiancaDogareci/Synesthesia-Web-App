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
        public string? Message { get; set; }

        public Guid? CurrentAudioId { get; set; }
        public bool IsCurrentAudioSaved { get; set; }

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

            try
            {
                var userId = User?.Identity?.IsAuthenticated == true
                    ? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty
                    : string.Empty;

                var audioRecord = new AudioFile
                {
                    UserId = userId,
                    FileName = audioFile.FileName,
                    FilePath = $"/uploads/audio/{newFileName}",
                    Format = ext.TrimStart('.')
                };

                _db.AudioFiles.Add(audioRecord);
                await _db.SaveChangesAsync();

                AudioPath = audioRecord.FilePath;
                CurrentAudioId = audioRecord.Id;
                IsCurrentAudioSaved = false;
                Message = "Uploaded successfully. Click 'Save to Profile' to add to your history.";
            }
            catch (Exception ex)
            {
                AudioPath = $"/uploads/audio/{newFileName}";
                Message = "Uploaded but failed to save record in DB: " + ex.Message;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveToProfileAsync(Guid audioId)
        {
            if (!User?.Identity?.IsAuthenticated ?? true)
            {
                Message = "You must be logged in to save audio to your profile.";
                return Page();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"Attempting to save audio {audioId} for user {userId}");

            try
            {
                var audioFile = await _db.AudioFiles
                    .FirstOrDefaultAsync(a => a.Id == audioId);

                if (audioFile == null)
                {
                    Message = "Audio file not found.";
                    Console.WriteLine($"Audio file {audioId} not found in database");
                    return Page();
                }

                Console.WriteLine($"Found audio file. Current UserId: '{audioFile.UserId}'");

                // Update the UserId if it was empty (anonymous upload)
                if (string.IsNullOrEmpty(audioFile.UserId) || audioFile.UserId == string.Empty)
                {
                    audioFile.UserId = userId;
                    audioFile.Update();
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"Updated audio file with UserId: {userId}");
                }
                else if (audioFile.UserId != userId)
                {
                    Message = "You don't have permission to save this audio.";
                    Console.WriteLine($"Permission denied. Audio UserId: {audioFile.UserId}, Current UserId: {userId}");
                    return Page();
                }

                AudioPath = audioFile.FilePath;
                CurrentAudioId = audioFile.Id;
                IsCurrentAudioSaved = true;
                Message = "Audio saved to your profile successfully!";
                Console.WriteLine("Audio saved successfully");
            }
            catch (Exception ex)
            {
                Message = "Failed to save audio to profile: " + ex.Message;
                Console.WriteLine($"Error saving audio: {ex.Message}");
            }

            return Page();
        }
    }
}