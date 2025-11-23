using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Synesthesia.Web.Models;

namespace Synesthesia.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {}

        public DbSet<AudioFile> AudioFiles { get; set; }
        public DbSet<SavedVideo> SavedVideos { get; set; }
    }
}
