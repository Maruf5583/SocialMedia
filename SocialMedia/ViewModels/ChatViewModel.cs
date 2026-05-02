using SocialMedia.Models;

namespace SocialMedia.ViewModels
{
    public class ChatViewModel
    {
        public string FriendId { get; set; } = string.Empty;
        public string FriendName { get; set; } = string.Empty;
        public string? FriendAvatar { get; set; }
        public List<Message> Messages { get; set; } = new();
        public string CurrentUserId { get; set; } = string.Empty;
    }
}
