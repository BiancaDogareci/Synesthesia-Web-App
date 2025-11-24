using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Synesthesia.Web.Models;

namespace Synesthesia.Web.Pages
{
    public class ProfileModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        public ProfileModel(UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public string? ProfilePicture { get; set; }
        public string? Username { get; set; }
        public string? Bio { get; set; }

        public async Task OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Username = user.UserName;
                ProfilePicture = user.ProfilePicture;
                Bio = user.Bio;
            }
        }
    }
}
