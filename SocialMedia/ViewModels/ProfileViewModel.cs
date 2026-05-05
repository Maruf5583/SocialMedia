using SocialMedia.Models;
using SocialMedia.ViewModels;

namespace SocialMedia.ViewModels
{
    public class ProfileViewModel
    {
        // User Info
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ProfilePicture { get; set; }

        // Edit Form
        public IFormFile? ProfilePictureFile { get; set; }

        // Friend Status
        public bool IsOwnProfile { get; set; }
        public bool IsFriend { get; set; }
        public bool HasPendingRequest { get; set; }
        public bool HasReceivedRequest { get; set; }
        public int? FriendRequestId { get; set; }

        // Counts
        public int FriendsCount { get; set; }
        public int PostsCount { get; set; }

        // Posts (NEW - যোগ করুন)
        public List<PostViewModel> Posts { get; set; } = new();

        // Current User (for SignalR)
        public string CurrentUserId { get; set; } = string.Empty;
    }
}