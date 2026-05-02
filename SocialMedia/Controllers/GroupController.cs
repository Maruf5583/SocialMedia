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
    [Authorize]
    public class GroupController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FriendService _friendService;
        private readonly FileUploadService _fileUploadService;

        public GroupController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            FriendService friendService,
            FileUploadService fileUploadService)
        {
            _db = db;
            _userManager = userManager;
            _friendService = friendService;
            _fileUploadService = fileUploadService;
        }

        // ─── All Groups ──────────────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var groups = await _db.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                    .ThenInclude(g => g.Members)
                .Include(gm => gm.Group)
                    .ThenInclude(g => g.CreatedBy)
                .Select(gm => gm.Group)
                .ToListAsync();

            return View(groups);
        }

        // ─── Create Group Page ───────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User)!;
            var friends = await _friendService.GetFriendsAsync(userId);

            var vm = new CreateGroupViewModel
            {
                Friends = friends.Select(f => new FriendCheckboxItem
                {
                    UserId = f.Id,
                    FullName = f.FullName,
                    Avatar = f.ProfilePicture
                }).ToList()
            };

            return View(vm);
        }

        // ─── Create Group POST ───────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGroupViewModel model)
        {
            var userId = _userManager.GetUserId(User)!;

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Group name is required.");
                model.Friends = (await _friendService.GetFriendsAsync(userId))
                    .Select(f => new FriendCheckboxItem
                    {
                        UserId = f.Id,
                        FullName = f.FullName,
                        Avatar = f.ProfilePicture
                    }).ToList();
                return View(model);
            }

            // Group Picture upload
            string? picturePath = null;
            if (model.GroupPictureFile != null)
            {
                try
                {
                    picturePath = await _fileUploadService
                        .UploadProfilePictureAsync(model.GroupPictureFile);
                }
                catch (InvalidOperationException ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                    return View(model);
                }
            }

            var group = new Group
            {
                Name = model.Name.Trim(),
                Description = model.Description,
                GroupPicture = picturePath,
                CreatedById = userId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Groups.Add(group);
            await _db.SaveChangesAsync();

            // Creator কে admin হিসেবে add করো
            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = group.Id,
                UserId = userId,
                IsAdmin = true,
                JoinedAt = DateTime.UtcNow
            });

            // Selected friends add করো
            var selectedFriends = model.SelectedFriendIds ?? new List<string>();
            foreach (var friendId in selectedFriends.Distinct())
            {
                if (friendId == userId) continue;

                // Friend কিনা verify করো
                var isFriend = await _friendService.AreFriendsAsync(userId, friendId);
                if (!isFriend) continue;

                _db.GroupMembers.Add(new GroupMember
                {
                    GroupId = group.Id,
                    UserId = friendId,
                    IsAdmin = false,
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Group \"{group.Name}\" created successfully!";
            return RedirectToAction("Chat", new { id = group.Id });
        }

        // ─── Group Chat Page ─────────────────────────────────────
        public async Task<IActionResult> Chat(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var group = await _db.Groups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .Include(g => g.CreatedBy)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            // Member কিনা check করো
            var isMember = group.Members.Any(m => m.UserId == userId);
            if (!isMember) return Forbid();

            // Messages আনো
            var messages = await _db.GroupMessages
                .Where(gm => gm.GroupId == id)
                .Include(gm => gm.Sender)
                .OrderBy(gm => gm.SentAt)
                .ToListAsync();

            var currentMember = group.Members.First(m => m.UserId == userId);

            var vm = new GroupChatViewModel
            {
                Group = group,
                Messages = messages,
                CurrentUserId = userId,
                IsAdmin = currentMember.IsAdmin
            };

            return View(vm);
        }

        // ─── Group Details / Members ─────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var group = await _db.Groups
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .Include(g => g.CreatedBy)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null) return NotFound();

            var isMember = group.Members.Any(m => m.UserId == userId);
            if (!isMember) return Forbid();

            var isAdmin = group.Members.Any(m => m.UserId == userId && m.IsAdmin);

            // Friends যারা এখনো member না
            var friends = await _friendService.GetFriendsAsync(userId);
            var memberIds = group.Members.Select(m => m.UserId).ToHashSet();
            var nonMemberFriends = friends.Where(f => !memberIds.Contains(f.Id)).ToList();

            var vm = new GroupDetailsViewModel
            {
                Group = group,
                IsAdmin = isAdmin,
                CurrentUserId = userId,
                NonMemberFriends = nonMemberFriends
            };

            return View(vm);
        }

        // ─── Add Member ──────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int groupId, string memberId)
        {
            var userId = _userManager.GetUserId(User)!;

            var isAdmin = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId &&
                                gm.UserId == userId &&
                                gm.IsAdmin);

            if (!isAdmin) return Forbid();

            var isFriend = await _friendService.AreFriendsAsync(userId, memberId);
            if (!isFriend)
            {
                TempData["Error"] = "You can only add friends to the group.";
                return RedirectToAction("Details", new { id = groupId });
            }

            var alreadyMember = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == memberId);

            if (!alreadyMember)
            {
                _db.GroupMembers.Add(new GroupMember
                {
                    GroupId = groupId,
                    UserId = memberId,
                    IsAdmin = false,
                    JoinedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Member added successfully!";
            }

            return RedirectToAction("Details", new { id = groupId });
        }

        // ─── Remove Member ───────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int groupId, string memberId)
        {
            var userId = _userManager.GetUserId(User)!;

            var isAdmin = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId &&
                                gm.UserId == userId &&
                                gm.IsAdmin);

            if (!isAdmin) return Forbid();

            // Creator কে remove করা যাবে না
            var group = await _db.Groups.FindAsync(groupId);
            if (group?.CreatedById == memberId)
            {
                TempData["Error"] = "Cannot remove the group creator.";
                return RedirectToAction("Details", new { id = groupId });
            }

            var member = await _db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == memberId);

            if (member != null)
            {
                _db.GroupMembers.Remove(member);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Member removed.";
            }

            return RedirectToAction("Details", new { id = groupId });
        }

        // ─── Leave Group ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Leave(int groupId)
        {
            var userId = _userManager.GetUserId(User)!;

            var group = await _db.Groups.FindAsync(groupId);

            // Creator cannot leave — must delete
            if (group?.CreatedById == userId)
            {
                TempData["Error"] = "You are the creator. Delete the group instead.";
                return RedirectToAction("Details", new { id = groupId });
            }

            var member = await _db.GroupMembers
                .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (member != null)
            {
                _db.GroupMembers.Remove(member);
                await _db.SaveChangesAsync();
            }

            TempData["Success"] = "You left the group.";
            return RedirectToAction("Index");
        }

        // ─── Delete Group ─────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int groupId)
        {
            var userId = _userManager.GetUserId(User)!;

            var group = await _db.Groups
                .Include(g => g.Members)
                .Include(g => g.Messages)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null) return NotFound();
            if (group.CreatedById != userId) return Forbid();

            // Picture delete করো
            _fileUploadService.DeleteFile(group.GroupPicture);

            _db.Groups.Remove(group);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Group deleted successfully.";
            return RedirectToAction("Index");
        }
    }
}