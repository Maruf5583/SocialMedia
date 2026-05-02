using SocialMedia.Models;

namespace SocialMedia.ViewModels
{
    public class GroupChatViewModel
    {
        public Group Group { get; set; } = null!;
        public List<GroupMessage> Messages { get; set; } = new();
        public string CurrentUserId { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}
