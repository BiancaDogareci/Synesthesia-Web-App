using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Synesthesia.Web.Models
{
    public class FractalProject : BaseEntity
    {
        public string UserId { get; set; }
        public virtual AppUser? User { get; set; }

        public Guid AudioId { get; set; }

        [ForeignKey(nameof(AudioId))]
        public AudioFile? AudioFile { get; set; }
        public string Title { get; set; }

        public string FractalType { get; set; }

        public string SettingsJson { get; set; }
    }
}
