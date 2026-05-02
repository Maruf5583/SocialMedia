using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;

namespace SocialMedia.Hubs
{
    [Authorize]
    public class GroupChatHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public GroupChatHub(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─── Group Room এ Join করো ───────────────────────────────
        public async Task JoinGroup(string groupId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            // Member কিনা verify করো
            var isMember = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == int.Parse(groupId) && gm.UserId == userId);

            if (!isMember) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, $"group_{groupId}");
        }

        // ─── Group Room থেকে বের হও ─────────────────────────────
        public async Task LeaveGroup(string groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group_{groupId}");
        }

        // ─── Group Message পাঠাও ────────────────────────────────
        public async Task SendGroupMessage(int groupId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var userId = _userManager.GetUserId(Context.User!)!;

            // Member কিনা check করো
            var isMember = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (!isMember) return;

            var sender = await _db.Users.FindAsync(userId);

            var message = new GroupMessage
            {
                GroupId = groupId,
                SenderId = userId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow
            };

            _db.GroupMessages.Add(message);
            await _db.SaveChangesAsync();

            var payload = new
            {
                id = message.Id,
                groupId = groupId,
                senderId = userId,
                senderName = sender!.FullName,
                senderAvatar = sender.ProfilePicture,
                content = message.Content,
                sentAt = message.SentAt.ToString("hh:mm tt")
            };

            // ওই group এর সবাইকে message পাঠাও
            await Clients.Group($"group_{groupId}")
                         .SendAsync("ReceiveGroupMessage", payload);
        }

        // ─── Typing Indicator ────────────────────────────────────
        public async Task GroupTyping(int groupId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            var sender = await _db.Users.FindAsync(userId);

            await Clients.OthersInGroup($"group_{groupId}")
                         .SendAsync("GroupUserTyping", userId, sender!.FullName);
        }

        public async Task GroupStopTyping(int groupId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            await Clients.OthersInGroup($"group_{groupId}")
                         .SendAsync("GroupUserStopTyping", userId);
        }
    }
}
