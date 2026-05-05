using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;

namespace SocialMedia.Hubs
{
    [Authorize]
    public class PostHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostHub(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─── React করো ──────────────────────────────────────────
        public async Task React(int postId, string reactionTypeStr)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            if (!Enum.TryParse<ReactionType>(reactionTypeStr, out var reactionType))
                return;

            var existing = await _db.PostReactions
                .FirstOrDefaultAsync(r => r.PostId == postId && r.UserId == userId);

            if (existing != null)
            {
                if (existing.ReactionType == reactionType)
                {
                    // Same reaction — remove (toggle off)
                    _db.PostReactions.Remove(existing);
                }
                else
                {
                    // Different reaction — update
                    existing.ReactionType = reactionType;
                }
            }
            else
            {
                // New reaction
                _db.PostReactions.Add(new PostReaction
                {
                    PostId = postId,
                    UserId = userId,
                    ReactionType = reactionType,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            // Updated reaction counts সবাইকে পাঠাও
            var counts = await _db.PostReactions
                .Where(r => r.PostId == postId)
                .GroupBy(r => r.ReactionType)
                .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                .ToListAsync();

            var totalCount = counts.Sum(c => c.Count);

            // আমার current reaction
            var myReaction = await _db.PostReactions
                .Where(r => r.PostId == postId && r.UserId == userId)
                .Select(r => (ReactionType?)r.ReactionType)
                .FirstOrDefaultAsync();

            await Clients.All.SendAsync("ReactionUpdated", new
            {
                postId,
                counts,
                totalCount,
                userId,
                myReaction = myReaction?.ToString()
            });
        }
    }
}