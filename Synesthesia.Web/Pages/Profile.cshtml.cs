using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Synesthesia.Web.Data;
using Synesthesia.Web.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace Synesthesia.Web.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ProfileModel(UserManager<AppUser> userManager, ApplicationDbContext db, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _db = db;
            _env = env;
        }

        public string? ProfilePicture { get; set; }
        public string? Username { get; set; }
        public string? Bio { get; set; }
        public List<AudioFile> AudioFiles { get; set; } = new List<AudioFile>();
        public List<FractalProject> Projects { get; set; } = new List<FractalProject>();

        public List<string>? DebugInfo { get; set; }

        public async Task OnGetAsync()
        {
            DebugInfo = new List<string>();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                DebugInfo.Add("User is NULL - not authenticated?");
                return;
            }

            Username = user.UserName;
            ProfilePicture = user.ProfilePicture;
            Bio = user.Bio;

            DebugInfo.Add($"User ID: {user.Id}");
            DebugInfo.Add($"Username: {user.UserName}");

            // Get ALL audio files first
            var allAudioFiles = await _db.AudioFiles.ToListAsync();
            DebugInfo.Add($"Total audio files in database: {allAudioFiles.Count}");

            // Show details of all audio files
            foreach (var af in allAudioFiles)
            {
                var userIdMatch = af.UserId == user.Id ? "MATCH!" : "no match";
                DebugInfo.Add($"  - File: {af.FileName}, UserId: '{af.UserId}' [{userIdMatch}]");
            }

            // Load user's saved audio files
            try
            {
                AudioFiles = await _db.AudioFiles
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.CreatedAt)
                    .ToListAsync();

                Projects = await _db.FractalProjects
                .Include(p => p.AudioFile)
                .Where(p => p.UserId == user.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

                DebugInfo.Add($"Projects for this user: {Projects.Count}");


                DebugInfo.Add($"Query completed. Audio files for this user: {AudioFiles.Count}");

                if (AudioFiles.Count > 0)
                {
                    DebugInfo.Add("First file details:");
                    var first = AudioFiles[0];
                    DebugInfo.Add($"  - ID: {first.Id}");
                    DebugInfo.Add($"  - FileName: {first.FileName}");
                    DebugInfo.Add($"  - FilePath: {first.FilePath}");
                    DebugInfo.Add($"  - CreatedAt: {first.CreatedAt}");
                }
            }
            catch (Exception ex)
            {
                DebugInfo.Add($"ERROR loading audio files: {ex.Message}");
                DebugInfo.Add($"Stack trace: {ex.StackTrace}");
            }
        }

        //public async Task<IActionResult> OnPostDeleteAudioAsync(System.Guid audioId)
        //{
        //    if (!User?.Identity?.IsAuthenticated ?? true)
        //    {
        //        return RedirectToPage("/Account/Login", new { area = "Identity" });
        //    }

        //    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        //    try
        //    {
        //        var audioFile = await _db.AudioFiles
        //            .FirstOrDefaultAsync(a => a.Id == audioId && a.UserId == userId);

        //        if (audioFile == null)
        //        {
        //            TempData["Error"] = "Audio file not found or you don't have permission to delete it.";
        //            return RedirectToPage();
        //        }

        //        // Delete the physical file
        //        var physicalPath = Path.Combine(_env.WebRootPath, audioFile.FilePath.TrimStart('/'));
        //        if (System.IO.File.Exists(physicalPath))
        //        {
        //            System.IO.File.Delete(physicalPath);
        //        }

        //        // Delete from database
        //        _db.AudioFiles.Remove(audioFile);
        //        await _db.SaveChangesAsync();

        //        TempData["Success"] = "Audio file deleted successfully.";
        //    }
        //    catch (System.Exception ex)
        //    {
        //        TempData["Error"] = "Failed to delete audio file: " + ex.Message;
        //    }

        //    return RedirectToPage();
        //}

        public async Task<IActionResult> OnPostDeleteProjectAsync(System.Guid projectId)
        {
            if (!User?.Identity?.IsAuthenticated ?? true)
                return RedirectToPage("/Account/Login", new { area = "Identity" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Load project + associated audio
                var project = await _db.FractalProjects
                    .Include(p => p.AudioFile)
                    .FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);

                if (project == null)
                {
                    TempData["Error"] = "Project not found or you don't have permission to delete it.";
                    return RedirectToPage();
                }

                var audioId = project.AudioId;
                var audioFile = project.AudioFile;

                // 1) Delete the project
                _db.FractalProjects.Remove(project);

                // 2) Decide whether to delete the audio too
                //    Only delete audio if no other projects reference it (safe behavior)
                var otherProjectsUsingAudio = await _db.FractalProjects
                    .AnyAsync(p => p.AudioId == audioId && p.Id != projectId);

                if (!otherProjectsUsingAudio && audioFile != null)
                {
                    // delete physical file
                    if (!string.IsNullOrWhiteSpace(audioFile.FilePath))
                    {
                        var physicalPath = Path.Combine(_env.WebRootPath, audioFile.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(physicalPath))
                            System.IO.File.Delete(physicalPath);
                    }

                    // delete audio row
                    _db.AudioFiles.Remove(audioFile);
                }

                await _db.SaveChangesAsync();

                TempData["Success"] = otherProjectsUsingAudio
                    ? "Project deleted. Audio kept (used by other projects)."
                    : "Project and audio deleted successfully.";
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = "Failed to delete: " + ex.Message;
            }

            return RedirectToPage();
        }

    }
}