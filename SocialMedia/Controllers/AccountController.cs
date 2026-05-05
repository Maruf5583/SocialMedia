using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;
using SocialMedia.Services;
using SocialMedia.ViewModels;

namespace SocialMedia.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly FriendService _friendService;
        private readonly FileUploadService _fileUploadService;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext db,
            FriendService friendService,
            FileUploadService fileUploadService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _friendService = friendService;
            _fileUploadService = fileUploadService;
        }

        // ─── Register ───────────────────────────────────────────
        [HttpGet]
        public IActionResult Register() =>
            User.Identity!.IsAuthenticated ? RedirectToAction("Index", "Home") : View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToAction("Index", "Home");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }

        // ─── Login ──────────────────────────────────────────────
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity!.IsAuthenticated) return RedirectToAction("Index", "Home");
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return LocalRedirect(returnUrl ?? "/");

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // ─── Logout ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // ─── Profile ─────────────────────────────────────────────
        [Authorize]
        [HttpGet]
        
        public async Task<IActionResult> Profile(string? id)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var userId = id ?? currentUserId;

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Get user's posts
            var posts = await _db.Posts
                .Include(p => p.User)
                .Include(p => p.Reactions)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .Include(p => p.OriginalPost)
                    .ThenInclude(op => op!.User)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Map posts to ViewModel
            var postVms = posts.Select(p => MapToPostViewModel(p, currentUserId, user.ProfilePicture, user.FullName)).ToList();

            // Check friend status
            var isFriend = await _db.FriendRequests
                .AnyAsync(f => f.Status == FriendRequestStatus.Accepted &&
                               ((f.SenderId == currentUserId && f.ReceiverId == userId) ||
                                (f.SenderId == userId && f.ReceiverId == currentUserId)));

            var pendingRequest = await _db.FriendRequests
                .FirstOrDefaultAsync(f => f.Status == FriendRequestStatus.Pending &&
                                          ((f.SenderId == currentUserId && f.ReceiverId == userId) ||
                                           (f.SenderId == userId && f.ReceiverId == currentUserId)));

            var friendsCount = await _db.FriendRequests
                .CountAsync(f => f.Status == FriendRequestStatus.Accepted &&
                                (f.SenderId == userId || f.ReceiverId == userId));

            var vm = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Bio = user.Bio,
                ProfilePicture = user.ProfilePicture,
                IsOwnProfile = currentUserId == userId,
                IsFriend = isFriend,
                HasPendingRequest = pendingRequest?.SenderId == currentUserId,
                HasReceivedRequest = pendingRequest?.ReceiverId == currentUserId,
                FriendRequestId = pendingRequest?.Id,
                FriendsCount = friendsCount,
                PostsCount = posts.Count,
                Posts = postVms,  // ← Posts যোগ করুন
                CurrentUserId = currentUserId
            };

            return View(vm);
        }

        // Helper method to map Post to PostViewModel
        private PostViewModel MapToPostViewModel(Post p, string currentUserId, string? currentUserAvatar, string? currentUserName)
        {
            var reactionCounts = p.Reactions
                .GroupBy(r => r.ReactionType.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            var myReaction = p.Reactions
                .FirstOrDefault(r => r.UserId == currentUserId)?.ReactionType;

            PostViewModel? originalVm = null;
            if (p.OriginalPost != null)
            {
                originalVm = new PostViewModel
                {
                    Id = p.OriginalPost.Id,
                    UserId = p.OriginalPost.UserId,
                    UserName = p.OriginalPost.User?.FullName ?? "Unknown",
                    UserAvatar = p.OriginalPost.User?.ProfilePicture,
                    Content = p.OriginalPost.Content,
                    ImagePath = p.OriginalPost.ImagePath,
                    IsEdited = p.OriginalPost.IsEdited,
                    CreatedAt = p.OriginalPost.CreatedAt,
                    TotalReactions = p.OriginalPost.Reactions.Count
                };
            }

            return new PostViewModel
            {
                Id = p.Id,
                UserId = p.UserId,
                UserName = p.User?.FullName ?? "Unknown",
                UserAvatar = p.User?.ProfilePicture,
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
                CurrentUserId = currentUserId,
                CurrentUserAvatar = currentUserAvatar,
                CurrentUserName = currentUserName,
                CommentCount = p.Comments?.Count ?? 0
            };
        }

        // ─── Edit Profile ────────────────────────────────────────
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            user.FullName = model.FullName;
            user.Bio = model.Bio;

            if (model.ProfilePictureFile != null)
            {
                try
                {
                    // Delete old picture
                    _fileUploadService.DeleteFile(user.ProfilePicture);

                    // Upload new picture
                    var path = await _fileUploadService.UploadProfilePictureAsync(model.ProfilePictureFile);
                    user.ProfilePicture = path;
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    model.Id = user.Id;
                    model.ProfilePicture = user.ProfilePicture;
                    model.IsOwnProfile = true;
                    return View("Profile", model);
                }
            }

            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }
    }
}
