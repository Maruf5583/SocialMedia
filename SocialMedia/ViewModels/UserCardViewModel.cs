namespace SocialMedia.ViewModels
{
    public class UserCardViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }
        public bool IsFriend { get; set; }
        public bool HasPendingRequestSent { get; set; }
        public bool HasPendingRequestReceived { get; set; }
        public int? FriendRequestId { get; set; }
    }
}
