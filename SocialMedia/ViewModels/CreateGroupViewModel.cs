using System.ComponentModel.DataAnnotations;

namespace SocialMedia.ViewModels
{
    public class CreateGroupViewModel
    {
        [Required]
        [Display(Name = "Group Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        public string? Description { get; set; }

        [Display(Name = "Group Picture")]
        public IFormFile? GroupPictureFile { get; set; }

        public List<string>? SelectedFriendIds { get; set; }
        public List<FriendCheckboxItem> Friends { get; set; } = new();
    }

    public class FriendCheckboxItem
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public bool IsSelected { get; set; }
    }
}
