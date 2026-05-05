namespace SocialMedia.Models
{
   
    public class Post
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public string? Content { get; set; }
        public string? ImagePath { get; set; }

        public bool IsEdited { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Share এর জন্য
        public int? OriginalPostId { get; set; }
        public Post? OriginalPost { get; set; }

        // Navigation
        public ICollection<PostReaction> Reactions { get; set; } = new List<PostReaction>();
        public ICollection<Post> Shares { get; set; } = new List<Post>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
