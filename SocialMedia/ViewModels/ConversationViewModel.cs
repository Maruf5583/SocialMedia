namespace SocialMedia.ViewModels
{
    public class ConversationViewModel
    {
        public string FriendId { get; set; } = string.Empty;
        public string FriendName { get; set; } = string.Empty;
        public string? FriendAvatar { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int UnreadCount { get; set; }
    }
}
