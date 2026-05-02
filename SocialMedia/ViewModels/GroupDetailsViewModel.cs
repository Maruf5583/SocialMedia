using SocialMedia.Models;

namespace SocialMedia.ViewModels
{
    public class GroupDetailsViewModel
    {
        public Group Group { get; set; } = null!;
        public bool IsAdmin { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public List<ApplicationUser> NonMemberFriends { get; set; } = new();
    }
}
