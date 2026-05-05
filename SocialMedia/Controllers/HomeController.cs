using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;
using SocialMedia.ViewModels;
using System.Diagnostics;

namespace SocialMedia.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var currentUser = await _db.Users.FindAsync(currentUserId);

            // ── Posts (সবার) ─────────────────────────────────────
            var posts = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.Reactions)
                .Include(p => p.Shares)
                .Include(p => p.Comments)          
        .ThenInclude(c => c.User)
                .Include(p => p.OriginalPost)
                    .ThenInclude(op => op!.User)
                .Include(p => p.OriginalPost)
                    .ThenInclude(op => op!.Reactions)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var postVms = posts.Select(p => MapToViewModel(p, currentUserId)).ToList();

            // ── Friends ───────────────────────────────────────────
            var friendIds = await _db.FriendRequests
                .Where(f => f.Status == FriendRequestStatus.Accepted &&
                            (f.SenderId == currentUserId || f.ReceiverId == currentUserId))
                .Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId)
                .ToListAsync();

            var friends = await _db.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            // ── People You May Know (not friends) ─────────────────
            var myRequests = await _db.FriendRequests
                .Where(f => f.SenderId == currentUserId || f.ReceiverId == currentUserId)
                .ToListAsync();

            var strangers = await _db.Users
                .Where(u => u.Id != currentUserId && !friendIds.Contains(u.Id))
                .Take(6)
                .ToListAsync();

            var peopleCards = strangers.Select(u =>
            {
                var req = myRequests.FirstOrDefault(f =>
                    (f.SenderId == currentUserId && f.ReceiverId == u.Id) ||
                    (f.SenderId == u.Id && f.ReceiverId == currentUserId));

                return new UserCardViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    ProfilePicture = u.ProfilePicture,
                    Bio = u.Bio,
                    IsFriend = false,
                    HasPendingRequestSent = req?.Status == FriendRequestStatus.Pending
                                              && req.SenderId == currentUserId,
                    HasPendingRequestReceived = req?.Status == FriendRequestStatus.Pending
                                              && req.ReceiverId == currentUserId,
                    FriendRequestId = req?.Id
                };
            }).ToList();

            // ── Pending Friend Requests ───────────────────────────
            var pendingRequests = await _db.FriendRequests
                .Include(f => f.Sender)
                .Where(f => f.ReceiverId == currentUserId &&
                            f.Status == FriendRequestStatus.Pending)
                .OrderByDescending(f => f.CreatedAt)
                .Take(5)
                .ToListAsync();

            var vm = new HomePageViewModel
            {
                Posts = postVms,
                Friends = friends,
                PeopleYouMayKnow = peopleCards,
                PendingRequests = pendingRequests,
                NewPost = new CreatePostViewModel(),
                CurrentUserId = currentUserId,
                CurrentUserName = currentUser!.FullName,
                CurrentUserAvatar = currentUser.ProfilePicture
            };

            return View(vm);
        }

        // ─── Error ───────────────────────────────────────────────
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id
                            ?? HttpContext.TraceIdentifier
            });
        }

        // ─── Helper: Post → ViewModel ─────────────────────────────
        private static PostViewModel MapToViewModel(Post p, string currentUserId)
        {
            var reactionCounts = p.Reactions
                .GroupBy(r => r.ReactionType.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            var myReaction = p.Reactions
                .FirstOrDefault(r => r.UserId == currentUserId)?.ReactionType;

            PostViewModel? originalVm = null;
            if (p.OriginalPost != null)
            {
                var origReactionCounts = p.OriginalPost.Reactions
                    .GroupBy(r => r.ReactionType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                originalVm = new PostViewModel
                {
                    Id = p.OriginalPost.Id,
                    UserId = p.OriginalPost.UserId,
                    UserName = p.OriginalPost.User.FullName,
                    UserAvatar = p.OriginalPost.User.ProfilePicture,
                    Content = p.OriginalPost.Content,
                    ImagePath = p.OriginalPost.ImagePath,
                    IsEdited = p.OriginalPost.IsEdited,
                    CreatedAt = p.OriginalPost.CreatedAt,
                    ReactionCounts = origReactionCounts,
                    TotalReactions = p.OriginalPost.Reactions.Count,
                    ShareCount = 0
                };
            }

            return new PostViewModel
            {
                Id = p.Id,
                UserId = p.UserId,
                UserName = p.User.FullName,
                UserAvatar = p.User.ProfilePicture,
                Content = p.Content,
                ImagePath = p.ImagePath,
                IsEdited = p.IsEdited,
                CreatedAt = p.CreatedAt,
                TotalReactions = p.Reactions.Count,
                ReactionCounts = reactionCounts,
                MyReaction = myReaction,
                ShareCount = p.Shares.Count,
                IsShared = p.OriginalPostId.HasValue,
                OriginalPost = originalVm,
                IsOwnPost = p.UserId == currentUserId,
                CurrentUserId = currentUserId
            };
        }
    }
}