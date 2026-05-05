using SocialMedia.Models;

namespace SocialMedia.ViewModels
{
    public class PostViewModel
    {
        public int Id { get; set; }

        // Author info
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }

        // Content
        public string? Content { get; set; }
        public string? ImagePath { get; set; }
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; }

        // Reactions
        public int TotalReactions { get; set; }
        public Dictionary<string, int> ReactionCounts { get; set; } = new();
        public ReactionType? MyReaction { get; set; }

        // Share
        public int ShareCount { get; set; }
        public bool IsShared { get; set; }
        public int? OriginalPostId { get; set; }
        public PostViewModel? OriginalPost { get; set; }

        // Flags
        public bool IsOwnPost { get; set; }
        public string CurrentUserId { get; set; } = string.Empty;
        public string? CurrentUserName { get; set; }  // ← যোগ করুন
        public string? CurrentUserAvatar { get; set; } // ← যোগ করুন

        // Comments
        public int CommentCount { get; set; }
        public List<CommentViewModel> Comments { get; set; } = new();
    }

    public class CreatePostViewModel
    {
        public string? Content { get; set; }
        public IFormFile? Image { get; set; }
    }

    public class EditPostViewModel
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        public string? ExistingImage { get; set; }
        public IFormFile? NewImage { get; set; }
        public bool RemoveImage { get; set; }
    }

    public class HomePageViewModel
    {
        public List<PostViewModel> Posts { get; set; } = new();
        public List<UserCardViewModel> PeopleYouMayKnow { get; set; } = new();
        public List<ApplicationUser> Friends { get; set; } = new();
        public List<FriendRequest> PendingRequests { get; set; } = new();
        public CreatePostViewModel NewPost { get; set; } = new();
        public string CurrentUserId { get; set; } = string.Empty;
        public string CurrentUserName { get; set; } = string.Empty;
        public string? CurrentUserAvatar { get; set; }
    }

    public class CommentViewModel
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? UserAvatar { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsEdited { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsOwnComment { get; set; }
        public int? ParentCommentId { get; set; }  // ← যোগ করুন (Reply এর জন্য)
        public List<CommentViewModel> Replies { get; set; } = new();  // ← যোগ করুন
    }

    public class AddCommentModel  // ← নাম ঠিক করুন (AddComment না)
    {
        public int PostId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
    }
}