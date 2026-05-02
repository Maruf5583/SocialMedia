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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public HomeController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var currentUserId = _userManager.GetUserId(User)!;

            var allUsers = await _db.Users
                .Where(u => u.Id != currentUserId)
                .ToListAsync();

            // Current user er sathe sob request
            var myRequests = await _db.FriendRequests
                .Where(f => f.SenderId == currentUserId || f.ReceiverId == currentUserId)
                .ToListAsync();

            var userCards = allUsers.Select(u =>
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
                    IsFriend = req?.Status == FriendRequestStatus.Accepted,
                    HasPendingRequestSent = req?.Status == FriendRequestStatus.Pending
                                           && req.SenderId == currentUserId,
                    HasPendingRequestReceived = req?.Status == FriendRequestStatus.Pending
                                               && req.ReceiverId == currentUserId,
                    FriendRequestId = req?.Id
                };
            }).ToList();

            return View(userCards);
        }
    }
}
