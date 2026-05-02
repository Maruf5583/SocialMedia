using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;
using SocialMedia.ViewModels;

namespace SocialMedia.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─── Messages main page ──────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User)!;

            // সব friends যাদের সাথে কথা হয়েছে বা হয়নি
            var friendIds = await _db.FriendRequests
                .Where(f => f.Status == FriendRequestStatus.Accepted &&
                            (f.SenderId == currentUserId || f.ReceiverId == currentUserId))
                .Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId)
                .ToListAsync();

            var friends = await _db.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();

            // প্রতিটা friend এর সাথে last message আনো
            var conversations = new List<ConversationViewModel>();

            foreach (var friend in friends)
            {
                var lastMessage = await _db.Messages
                    .Where(m =>
                        (m.SenderId == currentUserId && m.ReceiverId == friend.Id) ||
                        (m.SenderId == friend.Id && m.ReceiverId == currentUserId))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unreadCount = await _db.Messages
                    .CountAsync(m => m.SenderId == friend.Id &&
                                     m.ReceiverId == currentUserId &&
                                     !m.IsRead);

                conversations.Add(new ConversationViewModel
                {
                    FriendId = friend.Id,
                    FriendName = friend.FullName,
                    FriendAvatar = friend.ProfilePicture,
                    LastMessage = lastMessage?.Content,
                    LastMessageTime = lastMessage?.SentAt,
                    UnreadCount = unreadCount
                });
            }

            // Last message time দিয়ে sort করো
            conversations = conversations
                .OrderByDescending(c => c.LastMessageTime ?? DateTime.MinValue)
                .ToList();

            return View(conversations);
        }

        // ─── Specific user এর সাথে chat open করো ────────────────
        public async Task<IActionResult> Open(string userId)
        {
            var currentUserId = _userManager.GetUserId(User)!;

            // Friend কিনা check করো
            var areFriends = await _db.FriendRequests.AnyAsync(f =>
                f.Status == FriendRequestStatus.Accepted &&
                ((f.SenderId == currentUserId && f.ReceiverId == userId) ||
                 (f.SenderId == userId && f.ReceiverId == currentUserId)));

            if (!areFriends) return RedirectToAction("Index");

            var friend = await _db.Users.FindAsync(userId);
            if (friend == null) return NotFound();

            // পুরনো messages আনো
            var messages = await _db.Messages
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                    (m.SenderId == userId && m.ReceiverId == currentUserId))
                .Include(m => m.Sender)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Unread গুলো read mark করো
            var unread = messages
                .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
                .ToList();
            foreach (var m in unread) m.IsRead = true;
            if (unread.Any()) await _db.SaveChangesAsync();

            var vm = new ChatViewModel
            {
                FriendId = friend.Id,
                FriendName = friend.FullName,
                FriendAvatar = friend.ProfilePicture,
                Messages = messages,
                CurrentUserId = currentUserId
            };

            return View(vm);
        }
    }
}
