using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMedia.Data;
using SocialMedia.Migrations;
using SocialMedia.Models;
using SocialMedia.Services;
using SocialMedia.ViewModels;

namespace SocialMedia.Controllers
{
   
        [Authorize]
        public class PostController : Controller
        {
            private readonly ApplicationDbContext _db;
            private readonly UserManager<ApplicationUser> _userManager;
            private readonly FileUploadService _fileUploadService;

            public PostController(
                ApplicationDbContext db,
                UserManager<ApplicationUser> userManager,
                FileUploadService fileUploadService)
            {
                _db = db;
                _userManager = userManager;
                _fileUploadService = fileUploadService;
            }

            // ─── Create Post ─────────────────────────────────────────
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Create(CreatePostViewModel model)
            {
                var userId = _userManager.GetUserId(User)!;

                if (string.IsNullOrWhiteSpace(model.Content) && model.Image == null)
                {
                    TempData["Error"] = "Post must have text or an image.";
                    return RedirectToAction("Index", "Home");
                }

                string? imagePath = null;
                if (model.Image != null)
                {
                    try
                    {
                        imagePath = await _fileUploadService.UploadPostImageAsync(model.Image);
                    }
                    catch (InvalidOperationException ex)
                    {
                        TempData["Error"] = ex.Message;
                        return RedirectToAction("Index", "Home");
                    }
                }

                var post = new Post
                {
                    UserId = userId,
                    Content = model.Content?.Trim(),
                    ImagePath = imagePath,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Posts.Add(post);
                await _db.SaveChangesAsync();

                return RedirectToAction("Index", "Home");
            }

            // ─── Edit Post GET ────────────────────────────────────────
            [HttpGet]
            public async Task<IActionResult> Edit(int id)
            {
                var userId = _userManager.GetUserId(User)!;
                var post = await _db.Posts.FindAsync(id);

                if (post == null || post.UserId != userId)
                    return Forbid();

                var vm = new EditPostViewModel
                {
                    Id = post.Id,
                    Content = post.Content,
                    ExistingImage = post.ImagePath
                };

                return Json(vm);
            }

            // ─── Edit Post POST ───────────────────────────────────────
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Edit(EditPostViewModel model)
            {
                var userId = _userManager.GetUserId(User)!;
                var post = await _db.Posts.FindAsync(model.Id);

                if (post == null || post.UserId != userId)
                    return Forbid();

                if (string.IsNullOrWhiteSpace(model.Content) &&
                    model.NewImage == null &&
                    (model.RemoveImage || string.IsNullOrEmpty(post.ImagePath)))
                {
                    TempData["Error"] = "Post must have text or an image.";
                    return RedirectToAction("Index", "Home");
                }

                post.Content = model.Content?.Trim();
                post.IsEdited = true;
                post.UpdatedAt = DateTime.UtcNow;

                // Image remove করো
                if (model.RemoveImage && !string.IsNullOrEmpty(post.ImagePath))
                {
                    _fileUploadService.DeleteFile(post.ImagePath);
                    post.ImagePath = null;
                }

                // নতুন image upload করো
                if (model.NewImage != null)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(post.ImagePath))
                            _fileUploadService.DeleteFile(post.ImagePath);

                        post.ImagePath = await _fileUploadService
                            .UploadPostImageAsync(model.NewImage);
                    }
                    catch (InvalidOperationException ex)
                    {
                        TempData["Error"] = ex.Message;
                        return RedirectToAction("Index", "Home");
                    }
                }

                await _db.SaveChangesAsync();
                TempData["Success"] = "Post updated successfully!";
                return RedirectToAction("Index", "Home");
            }

            // ─── Delete Post ──────────────────────────────────────────
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Delete(int id)
            {
                var userId = _userManager.GetUserId(User)!;

                var post = await _db.Posts
                    .Include(p => p.Shares)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null) return NotFound();

                // Admin অথবা নিজের post
                var isAdmin = User.IsInRole("Admin");
                if (post.UserId != userId && !isAdmin)
                    return Forbid();

                // Image delete করো
                if (!string.IsNullOrEmpty(post.ImagePath))
                    _fileUploadService.DeleteFile(post.ImagePath);

                _db.Posts.Remove(post);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Post deleted.";
                return RedirectToAction("Index", "Home");
            }

            // ─── Share Post ───────────────────────────────────────────
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Share(int originalPostId, string? content)
            {
                var userId = _userManager.GetUserId(User)!;

                var original = await _db.Posts.FindAsync(originalPostId);
                if (original == null) return NotFound();

                // Original post এর original post আনো (chain share prevent)
                var rootPostId = original.OriginalPostId ?? originalPostId;

                var sharedPost = new Post
                {
                    UserId = userId,
                    Content = content?.Trim(),
                    OriginalPostId = rootPostId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Posts.Add(sharedPost);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Post shared!";
                return RedirectToAction("Index", "Home");
            }

        // ─── Get Comments for a Post ─────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetComments(int postId)
        {
            var comments = await _db.Comments
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.User)
                .Where(c => c.PostId == postId && c.ParentCommentId == null)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return PartialView("_CommentsList", comments);
        }


        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] AddCommentModel model)
        {
            try
            {
                Console.WriteLine($"=== DEBUG ADD COMMENT ===");
                Console.WriteLine($"Model null? {model == null}");

                if (model == null)
                {
                    Console.WriteLine("Model is null");
                    return Json(new { success = false, message = "Invalid request" });
                }

                Console.WriteLine($"PostId: {model.PostId}");
                Console.WriteLine($"Content: {model.Content}");

                if (string.IsNullOrWhiteSpace(model.Content))
                {
                    return Json(new { success = false, message = "Comment cannot be empty" });
                }

                var userId = _userManager.GetUserId(User);
                Console.WriteLine($"UserId: {userId}");

                if (string.IsNullOrEmpty(userId))
                {
                    return Json(new { success = false, message = "User not logged in" });
                }

                var comment = new Comment
                {
                    PostId = model.PostId,
                    UserId = userId,
                    Content = model.Content.Trim(),
                    ParentCommentId = model.ParentCommentId,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Comments.Add(comment);
                await _db.SaveChangesAsync();

                Console.WriteLine($"Comment saved with ID: {comment.Id}");

                return Ok(new   // ← Ok() ব্যবহার করুন
                {
                    success = true,
                    message = "Comment added!",
                    commentId = comment.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        // ─── Edit Comment ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int commentId, string content)
        {
            var userId = _userManager.GetUserId(User)!;
            var comment = await _db.Comments.FindAsync(commentId);

            if (comment == null)
                return Json(new { success = false, message = "Comment not found" });

            if (comment.UserId != userId)
                return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Comment cannot be empty" });

            comment.Content = content.Trim();
            comment.IsEdited = true;
            comment.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Json(new { success = true, content = comment.Content });
        }

        // ─── Delete Comment ───────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            var userId = _userManager.GetUserId(User)!;
            var comment = await _db.Comments
                .Include(c => c.Replies)
                .FirstOrDefaultAsync(c => c.Id == commentId);

            if (comment == null)
                return Json(new { success = false, message = "Comment not found" });

            var isAdmin = User.IsInRole("Admin");
            if (comment.UserId != userId && !isAdmin)
                return Json(new { success = false, message = "Unauthorized" });

            _db.Comments.Remove(comment);
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}
