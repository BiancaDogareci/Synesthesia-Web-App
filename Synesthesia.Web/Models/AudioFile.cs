using System;
using System.Collections.Generic;

namespace Synesthesia.Web.Models
{
    public class AudioFile : BaseEntity
    {
        public string UserId { get; set; }
        public virtual AppUser? User { get; set; }

        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string Format { get; set; }

        // 1 audio - many saved videos
        public virtual ICollection<SavedVideo>? SavedVideos { get; set; }
        public virtual ICollection<FractalProject>? FractalProjects { get; set; }

    }
}