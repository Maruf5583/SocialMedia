using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;

namespace SocialMedia.Services
{
    public class FriendService
    {
        private readonly ApplicationDbContext _db;

        public FriendService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<List<ApplicationUser>> GetFriendsAsync(string userId)
        {
            var friendIds = await _db.FriendRequests
                .Where(f => f.Status == FriendRequestStatus.Accepted &&
                            (f.SenderId == userId || f.ReceiverId == userId))
                .Select(f => f.SenderId == userId ? f.ReceiverId : f.SenderId)
                .ToListAsync();

            return await _db.Users
                .Where(u => friendIds.Contains(u.Id))
                .ToListAsync();
        }

        public async Task<bool> AreFriendsAsync(string userId1, string userId2)
        {
            return await _db.FriendRequests.AnyAsync(f =>
                f.Status == FriendRequestStatus.Accepted &&
                ((f.SenderId == userId1 && f.ReceiverId == userId2) ||
                 (f.SenderId == userId2 && f.ReceiverId == userId1)));
        }
         
        public async Task<FriendRequest?> GetPendingRequestAsync(string senderId, string receiverId)
        {
            return await _db.FriendRequests.FirstOrDefaultAsync(f =>
                f.Status == FriendRequestStatus.Pending &&
                ((f.SenderId == senderId && f.ReceiverId == receiverId) ||
                 (f.SenderId == receiverId && f.ReceiverId == senderId)));
        }
    }
}

