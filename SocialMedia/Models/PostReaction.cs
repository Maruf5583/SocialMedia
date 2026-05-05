namespace SocialMedia.Models
{
    public enum ReactionType
    {
        Like, Love, Haha, Wow, Sad, Angry
    }

    public class PostReaction
    {
        public int Id { get; set; }

        public int PostId { get; set; }
        public Post Post { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public ReactionType ReactionType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
