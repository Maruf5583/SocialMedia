using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;

namespace SocialMedia.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        // Online users track করার জন্য — userId -> connectionId
        private static readonly Dictionary<string, string> _onlineUsers = new();

        public ChatHub(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─── Connect হলে ─────────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            _onlineUsers[userId] = Context.ConnectionId;

            // সবাইকে জানাও এই user online হয়েছে
            await Clients.Others.SendAsync("UserOnline", userId);
            await base.OnConnectedAsync();
        }

        // ─── Disconnect হলে ──────────────────────────────────────
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            _onlineUsers.Remove(userId);

            
            await Clients.Others.SendAsync("UserOffline", userId);
            await base.OnDisconnectedAsync(exception);
        }

        // ─── Message পাঠানো ──────────────────────────────────────
        public async Task SendMessage(string receiverId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var senderId = _userManager.GetUserId(Context.User!)!;
            var sender = await _db.Users.FindAsync(senderId);

            // Friendship check
            var areFriends = await _db.FriendRequests.AnyAsync(f =>
                f.Status == FriendRequestStatus.Accepted &&
                ((f.SenderId == senderId && f.ReceiverId == receiverId) ||
                 (f.SenderId == receiverId && f.ReceiverId == senderId)));

            if (!areFriends) return;

            // Database এ save করো
            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content.Trim(),
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Messages.Add(message);
            await _db.SaveChangesAsync();

            var payload = new
            {
                id = message.Id,
                senderId = senderId,
                senderName = sender!.FullName,
                senderAvatar = sender.ProfilePicture,
                content = message.Content,
                sentAt = message.SentAt.ToString("hh:mm tt"),
                isRead = false
            };

            // Sender কে পাঠাও
            await Clients.Caller.SendAsync("ReceiveMessage", payload);

            // Receiver online থাকলে তাকেও পাঠাও
            if (_onlineUsers.TryGetValue(receiverId, out var receiverConn))
            {
                await Clients.Client(receiverConn).SendAsync("ReceiveMessage", payload);

                // Mark as read immediately যদি receiver active থাকে
                message.IsRead = true;
                await _db.SaveChangesAsync();
            }
        }

        // ─── Typing indicator ────────────────────────────────────
        public async Task Typing(string receiverId)
        {
            var senderId = _userManager.GetUserId(Context.User!)!;
            var sender = await _db.Users.FindAsync(senderId);

            if (_onlineUsers.TryGetValue(receiverId, out var receiverConn))
            {
                await Clients.Client(receiverConn)
                    .SendAsync("UserTyping", senderId, sender!.FullName);
            }
        }

        public async Task StopTyping(string receiverId)
        {
            var senderId = _userManager.GetUserId(Context.User!)!;

            if (_onlineUsers.TryGetValue(receiverId, out var receiverConn))
            {
                await Clients.Client(receiverConn).SendAsync("UserStopTyping", senderId);
            }
        }

        // ─── Messages read mark করা ──────────────────────────────
        public async Task MarkAsRead(string senderId)
        {
            var currentUserId = _userManager.GetUserId(Context.User!)!;

            var unread = await _db.Messages
                .Where(m => m.SenderId == senderId &&
                            m.ReceiverId == currentUserId &&
                            !m.IsRead)
                .ToListAsync();

            foreach (var m in unread) m.IsRead = true;
            await _db.SaveChangesAsync();

            // Sender কে জানাও messages read হয়েছে
            if (_onlineUsers.TryGetValue(senderId, out var senderConn))
            {
                await Clients.Client(senderConn)
                    .SendAsync("MessagesRead", currentUserId);
            }
        }

        // ─── Online users list দাও ───────────────────────────────
        public Task<List<string>> GetOnlineUsers()
        {
            return Task.FromResult(_onlineUsers.Keys.ToList());
        }
    }
}