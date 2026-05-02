using Microsoft.AspNetCore.Identity;

namespace SocialMedia.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ProfilePicture { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<FriendRequest> SentRequests { get; set; } = new List<FriendRequest>();
        public ICollection<FriendRequest> ReceivedRequests { get; set; } = new List<FriendRequest>();
        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    }
}
