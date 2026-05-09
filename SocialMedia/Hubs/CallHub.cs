using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;
using SocialMedia.Services;

namespace SocialMedia.Hubs
{
    [Authorize]
    public class CallHub : Hub
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly OnlineUserService _onlineUsers;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary
            <string, CallSession> _activeCalls = new();

        public CallHub(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            OnlineUserService onlineUsers)
        {
            _userManager = userManager;
            _db = db;
            _onlineUsers = onlineUsers;
        }

        // ─── Connect ─────────────────────────────────────────────
        public override async Task OnConnectedAsync()
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            _onlineUsers.AddUser(userId, Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        // ─── Disconnect ───────────────────────────────────────────
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            _onlineUsers.RemoveUser(userId);

            var activeCall = _activeCalls.Values
                .FirstOrDefault(c => c.Participants.Contains(userId));

            if (activeCall != null)
                await EndCallSession(activeCall.CallId, userId);

            await base.OnDisconnectedAsync(exception);
        }

        // ─── Private Call ─────────────────────────────────────────
        public async Task InitiateCall(string receiverId, bool isVideo)
        {
            var callerId = _userManager.GetUserId(Context.User!)!;
            var caller = await _db.Users.FindAsync(callerId);

            // Friend check
            var areFriends = await _db.FriendRequests.AnyAsync(f =>
                f.Status == FriendRequestStatus.Accepted &&
                ((f.SenderId == callerId && f.ReceiverId == receiverId) ||
                 (f.SenderId == receiverId && f.ReceiverId == callerId)));

            if (!areFriends)
            {
                await Clients.Caller.SendAsync("CallFailed", "You are not friends.");
                return;
            }

            // ✅ OnlineUserService দিয়ে check করো
            var receiverConn = _onlineUsers.GetConnectionId(receiverId);
            if (receiverConn == null)
            {
                await Clients.Caller.SendAsync("CallFailed", "User is offline.");
                return;
            }

            var callId = Guid.NewGuid().ToString();

            _activeCalls[callId] = new CallSession
            {
                CallId = callId,
                IsGroup = false,
                IsVideo = isVideo,
                Participants = new List<string> { callerId, receiverId }
            };

            await Clients.Client(receiverConn).SendAsync("IncomingCall", new
            {
                callId,
                callerId,
                callerName = caller!.FullName,
                callerAvatar = caller.ProfilePicture,
                isVideo
            });

            await Clients.Caller.SendAsync("CallInitiated", new { callId, isVideo });
        }

        // ─── Accept Call ──────────────────────────────────────────
        public async Task AcceptCall(string callId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            if (!_activeCalls.TryGetValue(callId, out var session)) return;

            var callerId = session.Participants.First(p => p != userId);
            var callerConn = _onlineUsers.GetConnectionId(callerId);

            if (callerConn != null)
                await Clients.Client(callerConn).SendAsync("CallAccepted", new { callId });

            await Clients.Caller.SendAsync("CallReady", new
            {
                callId,
                isVideo = session.IsVideo,
                isCaller = false
            });
        }

        // ─── Reject Call ──────────────────────────────────────────
        public async Task RejectCall(string callId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            if (!_activeCalls.TryGetValue(callId, out var session)) return;

            var callerId = session.Participants.First(p => p != userId);
            var callerConn = _onlineUsers.GetConnectionId(callerId);

            if (callerConn != null)
                await Clients.Client(callerConn)
                    .SendAsync("CallRejected", new { callId });

            _activeCalls.TryRemove(callId, out _);
        }

        // ─── End Call ─────────────────────────────────────────────
        public async Task EndCall(string callId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            await EndCallSession(callId, userId);
        }

        // ─── Group Call ───────────────────────────────────────────
        public async Task InitiateGroupCall(int groupId, bool isVideo)
        {
            var callerId = _userManager.GetUserId(Context.User!)!;
            var caller = await _db.Users.FindAsync(callerId);

            var isMember = await _db.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == callerId);

            if (!isMember) return;

            var group = await _db.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group == null) return;

            var callId = $"group_{groupId}_{Guid.NewGuid()}";
            var memberIds = group.Members.Select(m => m.UserId).ToList();

            _activeCalls[callId] = new CallSession
            {
                CallId = callId,
                IsGroup = true,
                IsVideo = isVideo,
                GroupId = groupId,
                Participants = memberIds
            };

            // Online members দের notify করো
            foreach (var memberId in memberIds.Where(m => m != callerId))
            {
                var memberConn = _onlineUsers.GetConnectionId(memberId);
                if (memberConn != null)
                {
                    await Clients.Client(memberConn).SendAsync("IncomingGroupCall", new
                    {
                        callId,
                        groupId,
                        groupName = group.Name,
                        callerId,
                        callerName = caller!.FullName,
                        callerAvatar = caller.ProfilePicture,
                        isVideo
                    });
                }
            }

            await Clients.Caller.SendAsync("GroupCallInitiated", new
            {
                callId,
                groupId,
                isVideo
            });

            await Groups.AddToGroupAsync(Context.ConnectionId, callId);
        }

        // ─── Group Call Join ──────────────────────────────────────
        public async Task JoinGroupCall(string callId)
        {
            var userId = _userManager.GetUserId(Context.User!)!;
            var user = await _db.Users.FindAsync(userId);

            if (!_activeCalls.TryGetValue(callId, out var session)) return;

            await Groups.AddToGroupAsync(Context.ConnectionId, callId);

            await Clients.OthersInGroup(callId).SendAsync("ParticipantJoined", new
            {
                userId,
                userName = user!.FullName,
                userAvatar = user.ProfilePicture,
                callId
            });

            var existingParticipants = session.Participants
                .Where(p => p != userId && _onlineUsers.IsOnline(p))
                .ToList();

            await Clients.Caller.SendAsync("ExistingParticipants", new
            {
                callId,
                participants = existingParticipants,
                isVideo = session.IsVideo
            });
        }

        // ─── WebRTC Signaling ─────────────────────────────────────
        public async Task SendOffer(string targetUserId, string callId, string sdp)
        {
            var senderId = _userManager.GetUserId(Context.User!)!;
            var targetConn = _onlineUsers.GetConnectionId(targetUserId);

            if (targetConn != null)
                await Clients.Client(targetConn).SendAsync("ReceiveOffer", new
                {
                    callId,
                    sdp,
                    fromUserId = senderId
                });
        }

        public async Task SendAnswer(string targetUserId, string callId, string sdp)
        {
            var senderId = _userManager.GetUserId(Context.User!)!;
            var targetConn = _onlineUsers.GetConnectionId(targetUserId);

            if (targetConn != null)
                await Clients.Client(targetConn).SendAsync("ReceiveAnswer", new
                {
                    callId,
                    sdp,
                    fromUserId = senderId
                });
        }

        public async Task SendIceCandidate(string targetUserId, string callId, string candidate)
        {
            var senderId = _userManager.GetUserId(Context.User!)!;
            var targetConn = _onlineUsers.GetConnectionId(targetUserId);

            if (targetConn != null)
                await Clients.Client(targetConn).SendAsync("ReceiveIceCandidate", new
                {
                    callId,
                    candidate,
                    fromUserId = senderId
                });
        }

        // ─── Media Toggle ─────────────────────────────────────────
        public async Task ToggleMedia(string callId, bool audioEnabled, bool videoEnabled)
        {
            var userId = _userManager.GetUserId(Context.User!)!;

            if (_activeCalls.TryGetValue(callId, out var session))
            {
                if (session.IsGroup)
                {
                    await Clients.OthersInGroup(callId)
                        .SendAsync("ParticipantMediaToggled", new
                        {
                            userId,
                            audioEnabled,
                            videoEnabled
                        });
                }
                else
                {
                    var otherId = session.Participants.FirstOrDefault(p => p != userId);
                    var otherConn = otherId != null
                        ? _onlineUsers.GetConnectionId(otherId)
                        : null;

                    if (otherConn != null)
                        await Clients.Client(otherConn)
                            .SendAsync("ParticipantMediaToggled", new
                            {
                                userId,
                                audioEnabled,
                                videoEnabled
                            });
                }
            }
        }

        // ─── Helper ───────────────────────────────────────────────
        private async Task EndCallSession(string callId, string userId)
        {
            if (!_activeCalls.TryGetValue(callId, out var session)) return;

            if (session.IsGroup)
            {
                await Clients.OthersInGroup(callId)
                    .SendAsync("ParticipantLeft", new { userId, callId });

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, callId);

                session.Participants.Remove(userId);
                if (!session.Participants.Any())
                    _activeCalls.TryRemove(callId, out _);
            }
            else
            {
                foreach (var participantId in
                    session.Participants.Where(p => p != userId))
                {
                    var conn = _onlineUsers.GetConnectionId(participantId);
                    if (conn != null)
                        await Clients.Client(conn)
                            .SendAsync("CallEnded", new { callId });
                }
                _activeCalls.TryRemove(callId, out _);
            }

            await Clients.Caller.SendAsync("CallEnded", new { callId });
        }
    }

    public class CallSession
    {
        public string CallId { get; set; } = string.Empty;
        public bool IsGroup { get; set; }
        public bool IsVideo { get; set; }
        public int? GroupId { get; set; }
        public List<string> Participants { get; set; } = new();
    }
}