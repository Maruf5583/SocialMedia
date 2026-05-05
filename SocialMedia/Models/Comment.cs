namespace SocialMedia.Models
{
    public class Comment
    {
        public int Id { get; set; }

        public string Content { get; set; } = string.Empty;

        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int? ParentCommentId { get; set; } // Reply ফিচারের জন্য
        public Comment? ParentComment { get; set; }

        public bool IsEdited { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public ICollection<Comment> Replies { get; set; } = new List<Comment>();
       public ICollection<CommentReaction> Reactions { get; set; } = new List<CommentReaction>();
    }

    public class CommentReaction
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
        public int CommentId { get; set; }
        public Comment Comment { get; set; } = null!;
        public ReactionType ReactionType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}