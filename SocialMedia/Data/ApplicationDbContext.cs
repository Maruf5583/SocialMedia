using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Models;

namespace SocialMedia.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }

        public DbSet<Post> Posts { get; set; }
        public DbSet<PostReaction> PostReactions { get; set; }

        public DbSet<Comment> Comments { get; set; }
        public DbSet<CommentReaction> CommentReactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ─────────────────────────────────────────────────────────────
            // FriendRequest Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<FriendRequest>()
                .HasOne(f => f.Sender)
                .WithMany(u => u.SentRequests)
                .HasForeignKey(f => f.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FriendRequest>()
                .HasOne(f => f.Receiver)
                .WithMany(u => u.ReceivedRequests)
                .HasForeignKey(f => f.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FriendRequest>()
                .HasIndex(f => new { f.SenderId, f.ReceiverId })
                .IsUnique();

            // ─────────────────────────────────────────────────────────────
            // Message Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─────────────────────────────────────────────────────────────
            // GroupMessage Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<GroupMessage>()
                .HasOne(gm => gm.Sender)
                .WithMany()
                .HasForeignKey(gm => gm.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─────────────────────────────────────────────────────────────
            // Group Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<Group>()
                .HasOne(g => g.CreatedBy)
                .WithMany()
                .HasForeignKey(g => g.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // ─────────────────────────────────────────────────────────────
            // GroupMember Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<GroupMember>()
                .HasIndex(gm => new { gm.GroupId, gm.UserId })
                .IsUnique();

            // ─────────────────────────────────────────────────────────────
            // Post Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<Post>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Post>()
                .HasOne(p => p.OriginalPost)
                .WithMany(p => p.Shares)
                .HasForeignKey(p => p.OriginalPostId)
                .OnDelete(DeleteBehavior.Restrict);

            // ─────────────────────────────────────────────────────────────
            // PostReaction Configuration
            // ─────────────────────────────────────────────────────────────
            builder.Entity<PostReaction>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PostReaction>()
                .HasOne(r => r.Post)
                .WithMany(p => p.Reactions)
                .HasForeignKey(r => r.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PostReaction>()
                .HasIndex(r => new { r.PostId, r.UserId })
                .IsUnique();

            // ─────────────────────────────────────────────────────────────
            // COMMENT Configuration (NEW)
            // ─────────────────────────────────────────────────────────────
            builder.Entity<Comment>(entity =>
            {
                // Primary Key
                entity.HasKey(c => c.Id);

                // Properties
                entity.Property(c => c.Content)
                    .IsRequired()
                    .HasMaxLength(2000);

                // Post Relationship - NO CASCADE (to avoid multiple cascade paths)
                entity.HasOne(c => c.Post)
                    .WithMany(p => p.Comments)
                    .HasForeignKey(c => c.PostId)
                    .OnDelete(DeleteBehavior.Restrict);  // ← Restrict, not Cascade

                // User Relationship
                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Self-Referencing for Replies - NO CASCADE
                entity.HasOne(c => c.ParentComment)
                    .WithMany(c => c.Replies)
                    .HasForeignKey(c => c.ParentCommentId)
                    .OnDelete(DeleteBehavior.Restrict);  // ← Restrict, not Cascade

                // Indexes for better performance
                entity.HasIndex(c => c.PostId);
                entity.HasIndex(c => c.ParentCommentId);
                entity.HasIndex(c => c.UserId);
                entity.HasIndex(c => c.CreatedAt);
            });

            // ─────────────────────────────────────────────────────────────
            // COMMENT REACTION Configuration (NEW)
            // ─────────────────────────────────────────────────────────────
            builder.Entity<CommentReaction>(entity =>
            {
                // Primary Key
                entity.HasKey(cr => cr.Id);

                // User Relationship
                entity.HasOne(cr => cr.User)
                    .WithMany()
                    .HasForeignKey(cr => cr.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Comment Relationship - Cascade (when comment is deleted, reactions are deleted)
                entity.HasOne(cr => cr.Comment)
                    .WithMany(c => c.Reactions)
                    .HasForeignKey(cr => cr.CommentId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Unique constraint: one reaction per user per comment
                entity.HasIndex(cr => new { cr.CommentId, cr.UserId })
                    .IsUnique();

                // Indexes
                entity.HasIndex(cr => cr.ReactionType);
            });
        }
    }
}