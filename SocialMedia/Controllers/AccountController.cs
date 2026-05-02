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
        public async Task<IActionResult> Profile(string? id = null)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var targetId = id ?? currentUserId;

            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetId);
            if (targetUser == null) return NotFound();

            var isOwn = targetId == currentUserId;
            var friends = await _friendService.GetFriendsAsync(targetId);
            var pendingRequest = await _friendService.GetPendingRequestAsync(currentUserId, targetId);

            var vm = new ProfileViewModel
            {
                Id = targetUser.Id,
                FullName = targetUser.FullName,
                Bio = targetUser.Bio,
                ProfilePicture = targetUser.ProfilePicture,
                IsOwnProfile = isOwn,
                FriendsCount = friends.Count,
                IsFriend = await _friendService.AreFriendsAsync(currentUserId, targetId),
                HasPendingRequest = pendingRequest != null && pendingRequest.SenderId == currentUserId,
                HasReceivedRequest = pendingRequest != null && pendingRequest.SenderId == targetId,
                FriendRequestId = pendingRequest?.Id ?? 0
            };

            return View(vm);
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
