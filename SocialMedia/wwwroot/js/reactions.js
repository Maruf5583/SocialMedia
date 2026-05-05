// ──────────────────────────────────────────────────────────────
// Reaction System
// ──────────────────────────────────────────────────────────────
const reactionTimers = {};

function showReactions(postId) {
    cancelHideReactions(postId);
    const popup = document.getElementById(`reaction-popup-${postId}`);
    if (popup) popup.classList.remove("d-none");
}

function scheduleHideReactions(postId) {
    reactionTimers[postId] = setTimeout(() => hideReactions(postId), 300);
}

function cancelHideReactions(postId) {
    if (reactionTimers[postId]) {
        clearTimeout(reactionTimers[postId]);
        delete reactionTimers[postId];
    }
}

function hideReactions(postId) {
    const popup = document.getElementById(`reaction-popup-${postId}`);
    if (popup) popup.classList.add("d-none");
}

// ──────────────────────────────────────────────────────────────
// React to Post (SignalR)
// ──────────────────────────────────────────────────────────────
function reactToPost(postId, reactionType) {
    if (window.postConnection && window.postConnection.state === signalR.HubConnectionState.Connected) {
        window.postConnection.invoke("React", postId, reactionType)
            .catch(err => console.error("Error invoking React:", err));
    } else {
        console.error("SignalR connection is not ready");
        // Fallback: AJAX call if SignalR is not available
        fetch(`/Post/React`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify({ postId: postId, reactionType: reactionType })
        })
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    updateReactionUI(postId, data);
                }
            })
            .catch(error => console.error("Error:", error));
    }
    hideReactions(postId);
}

// Update reaction UI (for AJAX fallback)
function updateReactionUI(postId, data) {
    const reactionSummary = document.getElementById(`reaction-summary-${postId}`);
    const reactionTotal = document.getElementById(`reaction-total-${postId}`);
    const reactButton = document.querySelector(`button[onclick*="reactToPost(${postId}"]`);

    if (reactionTotal && data.totalCount !== undefined) {
        reactionTotal.textContent = data.totalCount > 0 ? data.totalCount : "";
    }

    if (reactionSummary && data.counts) {
        const emojiMap = {
            Like: "👍", Love: "❤️", Haha: "😂",
            Wow: "😮", Sad: "😢", Angry: "😡"
        };
        let html = "";
        data.counts.forEach(c => {
            html += `<span class="me-1">${emojiMap[c.type] || ""}</span>`;
        });
        reactionSummary.innerHTML = html;
    }

    if (reactButton && data.myReaction) {
        // Update button text
        const emojiMap = {
            Like: "👍", Love: "❤️", Haha: "😂",
            Wow: "😮", Sad: "😢", Angry: "😡"
        };
        reactButton.innerHTML = `<span>${emojiMap[data.myReaction] || "👍"} ${data.myReaction}</span>`;
        reactButton.classList.add("active-reaction");
    }
}

// ──────────────────────────────────────────────────────────────
// Comment System Functions
// ──────────────────────────────────────────────────────────────
let commentTimers = {};

function toggleComments(postId) {
    const section = document.getElementById(`commentsSection-${postId}`);
    if (!section) return;

    if (section.classList.contains('d-none')) {
        section.classList.remove('d-none');
        loadComments(postId);
    } else {
        section.classList.add('d-none');
    }
}

function loadComments(postId) {
    const commentsList = document.getElementById(`commentsList-${postId}`);
    if (!commentsList) return;

    commentsList.innerHTML = `
        <div class="text-center text-muted small py-2">
            <div class="spinner-border spinner-border-sm text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
            <span class="ms-2">Loading comments...</span>
        </div>
    `;

    fetch(`/Post/GetComments?postId=${postId}`)
        .then(response => response.text())
        .then(html => {
            commentsList.innerHTML = html;
        })
        .catch(error => {
            console.error('Error loading comments:', error);
            commentsList.innerHTML = `
                <div class="text-center text-danger small py-2">
                    <i class="bi bi-exclamation-triangle"></i> Failed to load comments
                </div>
            `;
        });
}

function addComment(postId, parentCommentId = null) {
    const textarea = document.getElementById(`commentText-${postId}`);
    if (!textarea) {
        console.error('Textarea not found');
        alert('Could not find comment input');
        return;
    }

    const content = textarea.value.trim();
    if (!content) {
        alert('Please enter a comment');
        return;
    }

    const commentData = {
        postId: postId,
        content: content,
        parentCommentId: parentCommentId
    };

    console.log('Sending data:', JSON.stringify(commentData));

    fetch('/Post/AddComment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(commentData)
    })
        .then(response => {
            console.log('Response status:', response.status);
            console.log('Response OK?', response.ok);

            // Check if response is OK
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            // Check if response has content
            const contentLength = response.headers.get('content-length');
            if (contentLength === '0') {
                throw new Error('Empty response from server');
            }

            return response.text();  // First get as text
        })
        .then(text => {
            console.log('Raw response text:', text);

            if (!text || text.trim() === '') {
                throw new Error('Empty response');
            }

            try {
                const data = JSON.parse(text);
                console.log('Parsed data:', data);

                if (data.success) {
                    textarea.value = '';
                    loadComments(postId);
                    alert('Comment added successfully!');
                } else {
                    alert(data.message || 'Failed to add comment');
                }
            } catch (e) {
                console.error('JSON parse error:', e);
                alert('Invalid response from server');
            }
        })
        .catch(error => {
            console.error('Fetch error:', error);
            alert('Error: ' + error.message);
        });
}
function editComment(commentId, currentContent) {
    const newContent = prompt('Edit your comment:', currentContent);
    if (!newContent || newContent.trim() === currentContent) return;

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Post/EditComment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify({
            commentId: commentId,
            content: newContent.trim()
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const commentElement = document.getElementById(`comment-${commentId}`);
                if (commentElement) {
                    const textElement = commentElement.querySelector('.comment-text');
                    if (textElement) textElement.textContent = data.content;

                    const timeElement = commentElement.querySelector('.comment-time');
                    if (timeElement && !timeElement.innerHTML.includes('(edited)')) {
                        timeElement.innerHTML += ' <i>(edited)</i>';
                    }
                    showToast('Comment updated!', 'success');
                }
            } else {
                showToast(data.message || 'Failed to edit comment', 'error');
            }
        })
        .catch(error => {
            console.error('Error editing comment:', error);
            showToast('An error occurred. Please try again.', 'error');
        });
}

function deleteComment(commentId) {
    if (!confirm('Are you sure you want to delete this comment?')) return;

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch(`/Post/DeleteComment?commentId=${commentId}`, {
        method: 'POST',
        headers: {
            'RequestVerificationToken': token
        }
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                const commentElement = document.getElementById(`comment-${commentId}`);
                if (commentElement) {
                    const postId = commentElement.dataset.postId;
                    loadComments(postId);
                    showToast('Comment deleted!', 'success');
                }
            } else {
                showToast(data.message || 'Failed to delete comment', 'error');
            }
        })
        .catch(error => {
            console.error('Error deleting comment:', error);
            showToast('An error occurred. Please try again.', 'error');
        });
}

function showReplyForm(commentId, userName) {
    const replyFormContainer = document.getElementById(`reply-form-${commentId}`);
    if (!replyFormContainer) return;

    if (replyFormContainer.classList.contains('d-none')) {
        // Hide any other open reply forms
        document.querySelectorAll('.reply-form-container').forEach(container => {
            if (container.id !== `reply-form-${commentId}`) {
                container.classList.add('d-none');
                const textarea = container.querySelector('textarea');
                if (textarea) textarea.value = '';
            }
        });

        replyFormContainer.classList.remove('d-none');
        const textarea = document.getElementById(`reply-text-${commentId}`);
        if (textarea) {
            textarea.focus();
            textarea.placeholder = `Reply to ${userName}...`;
        }
    } else {
        replyFormContainer.classList.add('d-none');
        const textarea = document.getElementById(`reply-text-${commentId}`);
        if (textarea) textarea.value = '';
    }
}

function cancelReply(commentId) {
    const replyFormContainer = document.getElementById(`reply-form-${commentId}`);
    if (replyFormContainer) {
        replyFormContainer.classList.add('d-none');
        const textarea = document.getElementById(`reply-text-${commentId}`);
        if (textarea) textarea.value = '';
    }
}

function submitReply(commentId, postId, userName) {
    const textarea = document.getElementById(`reply-text-${commentId}`);
    if (!textarea) return;

    const content = textarea.value.trim();
    if (!content) {
        showToast('Please enter a reply', 'warning');
        return;
    }

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    fetch('/Post/AddComment', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': token
        },
        body: JSON.stringify({
            postId: postId,
            content: content,
            parentCommentId: commentId
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                textarea.value = '';
                document.getElementById(`reply-form-${commentId}`)?.classList.add('d-none');
                loadComments(postId);
                showToast('Reply added!', 'success');
            } else {
                showToast(data.message || 'Failed to add reply', 'error');
            }
        })
        .catch(error => {
            console.error('Error adding reply:', error);
            showToast('An error occurred. Please try again.', 'error');
        });
}

function cancelComment(postId) {
    const textarea = document.getElementById(`commentText-${postId}`);
    if (textarea) textarea.value = '';

    const section = document.getElementById(`commentsSection-${postId}`);
    if (section) section.classList.add('d-none');
}

// ──────────────────────────────────────────────────────────────
// Toast Notification Helper
// ──────────────────────────────────────────────────────────────
function showToast(message, type = 'info') {
    // Check if toast container exists, if not create it
    let toastContainer = document.getElementById('toast-container');
    if (!toastContainer) {
        toastContainer = document.createElement('div');
        toastContainer.id = 'toast-container';
        toastContainer.style.position = 'fixed';
        toastContainer.style.bottom = '20px';
        toastContainer.style.right = '20px';
        toastContainer.style.zIndex = '1050';
        document.body.appendChild(toastContainer);
    }

    const toastId = 'toast-' + Date.now();
    const bgClass = type === 'success' ? 'bg-success' :
        type === 'error' ? 'bg-danger' :
            type === 'warning' ? 'bg-warning' : 'bg-info';
    const textClass = type === 'warning' ? 'text-dark' : 'text-white';

    const toastHtml = `
        <div id="${toastId}" class="toast align-items-center ${bgClass} ${textClass} border-0 mb-2" role="alert">
            <div class="d-flex">
                <div class="toast-body">
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        </div>
    `;

    toastContainer.insertAdjacentHTML('beforeend', toastHtml);
    const toastElement = document.getElementById(toastId);
    const toast = new bootstrap.Toast(toastElement, { delay: 3000 });
    toast.show();

    // Remove toast after it's hidden
    toastElement.addEventListener('hidden.bs.toast', () => {
        toastElement.remove();
    });
}

// ──────────────────────────────────────────────────────────────
// Initialize SignalR Connection
// ──────────────────────────────────────────────────────────────
function initializeSignalR(hubUrl, currentUserId) {
    if (window.postConnection) {
        return window.postConnection;
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    connection.on("ReactionUpdated", (data) => {
        // Update total count
        const totalEl = document.getElementById(`reaction-total-${data.postId}`);
        if (totalEl) totalEl.textContent = data.totalCount || "";

        // Update emoji summary
        const summaryEl = document.getElementById(`reaction-summary-${data.postId}`);
        if (summaryEl && data.counts) {
            const emojiMap = {
                Like: "👍", Love: "❤️", Haha: "😂",
                Wow: "😮", Sad: "😢", Angry: "😡"
            };
            let html = "";
            data.counts.forEach(c => {
                html += `<span class="me-1">${emojiMap[c.type] || ""}</span>`;
            });
            summaryEl.innerHTML = html;
        }

        // Update my reaction button
        if (data.userId === currentUserId && data.myReaction) {
            document.querySelectorAll(`.reaction-btn-${data.postId}`)
                .forEach(btn => btn.classList.remove("active-reaction"));

            const activeBtn = document.getElementById(`react-${data.postId}-${data.myReaction}`);
            if (activeBtn) {
                activeBtn.classList.add("active-reaction");
                // Update button text if it's the main button
                const mainBtn = document.querySelector(`button[onclick*="reactToPost(${data.postId}"]`);
                if (mainBtn && !mainBtn.closest('.reaction-popup')) {
                    const emojiMap = {
                        Like: "👍", Love: "❤️", Haha: "😂",
                        Wow: "😮", Sad: "😢", Angry: "😡"
                    };
                    mainBtn.innerHTML = `<span>${emojiMap[data.myReaction] || "👍"} ${data.myReaction}</span>`;
                }
            }
        } else if (data.userId === currentUserId && !data.myReaction) {
            // User removed their reaction
            const mainBtn = document.querySelector(`button[onclick*="reactToPost(${data.postId}"]`);
            if (mainBtn && !mainBtn.closest('.reaction-popup')) {
                mainBtn.innerHTML = `<span><i class="bi bi-hand-thumbs-up"></i> Like</span>`;
            }
        }
    });

    connection.start()
        .then(() => {
            console.log("SignalR connected");
        })
        .catch(err => {
            console.error("SignalR connection error:", err);
            // Retry after 5 seconds
            setTimeout(() => initializeSignalR(hubUrl, currentUserId), 5000);
        });

    window.postConnection = connection;
    return connection;
}

// ──────────────────────────────────────────────────────────────
// DOM Ready Event
// ──────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    // Close reaction popups when clicking outside
    document.addEventListener('click', function (event) {
        if (!event.target.closest('[onmouseenter*="showReactions"]') &&
            !event.target.closest('.reaction-popup')) {
            document.querySelectorAll('.reaction-popup').forEach(popup => {
                popup.classList.add('d-none');
            });
        }
    });

    // Auto-expand textareas
    document.querySelectorAll('textarea').forEach(textarea => {
        textarea.addEventListener('input', function () {
            this.style.height = 'auto';
            this.style.height = Math.min(this.scrollHeight, 120) + 'px';
        });
    });
});