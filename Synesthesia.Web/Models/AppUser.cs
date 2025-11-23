using Microsoft.AspNetCore.Identity;

namespace Synesthesia.Web.Models
{
    public class AppUser : IdentityUser
    {
        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }

        // Relationships 1-M
        public virtual ICollection<AudioFile>? AudioFiles { get; set; }
        public virtual ICollection<SavedVideo>? SavedVideos { get; set; }
    }
}