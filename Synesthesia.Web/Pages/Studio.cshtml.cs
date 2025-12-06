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

        public void OnGet()
        {

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

            // Ensure upload dir exists
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

            // Save DB record
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
                Message = "Uploaded successfully.";
            }
            catch (Exception ex)
            {
                // still expose the file path even if DB save failed
                AudioPath = $"/uploads/audio/{newFileName}";
                Message = "Uploaded but failed to save record in DB: " + ex.Message;
            }

            return Page();
        }
    }
}
