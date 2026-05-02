using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Models;

namespace SocialMedia.Controllers
{
  
    [Authorize]
    public class FriendController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public FriendController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ─── Send Request ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendRequest(string receiverId)
        {
            var senderId = _userManager.GetUserId(User)!;

            if (senderId == receiverId)
                return BadRequest();

            // Already exists check
            var exists = await _db.FriendRequests.AnyAsync(f =>
                (f.SenderId == senderId && f.ReceiverId == receiverId) ||
                (f.SenderId == receiverId && f.ReceiverId == senderId));

            if (!exists)
            {
                _db.FriendRequests.Add(new FriendRequest
                {
                    SenderId = senderId,
                    ReceiverId = receiverId
                });
                await _db.SaveChangesAsync();
            }

            return RedirectBack();
        }

        // ─── Accept Request ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int requestId)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var request = await _db.FriendRequests.FindAsync(requestId);

            if (request == null || request.ReceiverId != currentUserId)
                return NotFound();

            request.Status = FriendRequestStatus.Accepted;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectBack();
        }

        // ─── Reject Request ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(int requestId)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var request = await _db.FriendRequests.FindAsync(requestId);

            if (request == null || request.ReceiverId != currentUserId)
                return NotFound();

            request.Status = FriendRequestStatus.Rejected;
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectBack();
        }

        // ─── Cancel Request ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int requestId)
        {
            var currentUserId = _userManager.GetUserId(User)!;
            var request = await _db.FriendRequests.FindAsync(requestId);

            if (request == null || request.SenderId != currentUserId)
                return NotFound();

            _db.FriendRequests.Remove(request);
            await _db.SaveChangesAsync();

            return RedirectBack();
        }

        // ─── Unfriend ────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfriend(string friendId)
        {
            var currentUserId = _userManager.GetUserId(User)!;

            var request = await _db.FriendRequests.FirstOrDefaultAsync(f =>
                f.Status == FriendRequestStatus.Accepted &&
                ((f.SenderId == currentUserId && f.ReceiverId == friendId) ||
                 (f.SenderId == friendId && f.ReceiverId == currentUserId)));

            if (request != null)
            {
                _db.FriendRequests.Remove(request);
                await _db.SaveChangesAsync();
            }

            return RedirectBack();
        }

        // ─── Friend Requests Inbox ───────────────────────────────
        public async Task<IActionResult> Requests()
        {
            var currentUserId = _userManager.GetUserId(User)!;

            var requests = await _db.FriendRequests
                .Include(f => f.Sender)
                .Where(f => f.ReceiverId == currentUserId
                         && f.Status == FriendRequestStatus.Pending)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return View(requests);
        }

        private IActionResult RedirectBack()
        {
            var referer = Request.Headers["Referer"].ToString();
            return string.IsNullOrEmpty(referer)
                ? RedirectToAction("Index", "Home")
                : Redirect(referer);
        }
    }
}
