using System;

namespace Synesthesia.Web.Models
{
    public class SavedVideo : BaseEntity
    {
        public string UserId { get; set; }
        public virtual AppUser? User { get; set; }

        public Guid AudioId { get; set; }
        public virtual AudioFile? AudioFile { get; set; }

        public string VideoPath { get; set; }
        public string Title { get; set; }
    }
}