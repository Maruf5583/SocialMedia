using System.ComponentModel.DataAnnotations;

namespace SocialMedia.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required, Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Bio")]
        [MaxLength(300)]
        public string? Bio { get; set; }

        public string? ProfilePicture { get; set; }

        [Display(Name = "Profile Picture")]
        public IFormFile? ProfilePictureFile { get; set; }

        public bool IsFriend { get; set; }
        public bool HasPendingRequest { get; set; }
        public bool HasReceivedRequest { get; set; }
        public int FriendRequestId { get; set; }
        public int FriendsCount { get; set; }
        public bool IsOwnProfile { get; set; }
    }
}
