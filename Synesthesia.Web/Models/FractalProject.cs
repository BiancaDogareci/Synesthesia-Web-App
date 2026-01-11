using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Synesthesia.Web.Models
{
    public class FractalProject : BaseEntity
    {
        public string UserId { get; set; }
        public virtual AppUser? User { get; set; }

        // This is your FK
        public Guid AudioId { get; set; }

        // Tell EF this navigation uses AudioId
        [ForeignKey(nameof(AudioId))]
        public AudioFile? AudioFile { get; set; }
        public string Title { get; set; }

        // e.g. "julia" | "mandelbrot" | "mandelbulb"
        public string FractalType { get; set; }

        // JSON snapshot of all the UI/settings state
        public string SettingsJson { get; set; }
    }
}
