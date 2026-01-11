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
        public DbSet<FractalProject> FractalProjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FractalProject>()
                .HasOne(p => p.AudioFile)
                .WithMany(a => a.FractalProjects)
                .HasForeignKey(p => p.AudioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SavedVideo>()
                .HasOne(v => v.AudioFile)
                .WithMany(a => a.SavedVideos)
                .HasForeignKey(v => v.AudioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
