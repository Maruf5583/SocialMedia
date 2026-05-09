// ════════════════════════════════════════════════════
//  WebRTC + SignalR Call Manager
// ════════════════════════════════════════════════════

const CallManager = (() => {

    // ─── State ───────────────────────────────────────
    let connection = null;
    let localStream = null;
    let peerConnections = {};
    let currentCallId = null;
    let currentUserId = null;
    let isVideo = false;
    let audioEnabled = true;
    let videoEnabled = true;
    let isGroup = false;
    let callTimer = null;
    let callSeconds = 0;

    // ─── STUN Config ─────────────────────────────────
    const iceConfig = {
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" },
            { urls: "stun:stun1.l.google.com:19302" },
            { urls: "stun:stun2.l.google.com:19302" }
        ]
    };

    // ════════════════════════════════════════════════
    //  INIT
    // ════════════════════════════════════════════════
    async function init(userId) {
        currentUserId = userId;

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/callHub")
            .withAutomaticReconnect()
            .build();

        registerHandlers();

        try {
            await connection.start();
            console.log("✅ CallHub connected.");
        } catch (err) {
            console.error("❌ CallHub connection failed:", err);
        }
    }

    // ════════════════════════════════════════════════
    //  SignalR EVENT HANDLERS
    // ════════════════════════════════════════════════
    function registerHandlers() {

        // ─── Private Call ─────────────────────────────
        connection.on("CallInitiated", (data) => {
            console.log("CallInitiated:", data);
            currentCallId = data.callId;
            isVideo = data.isVideo;
            isGroup = false;
            showCallingUI();
        });

        connection.on("IncomingCall", (data) => {
            console.log("IncomingCall:", data);
            showIncomingCallUI(data);
        });

        connection.on("CallAccepted", async (data) => {
            console.log("CallAccepted:", data);
            hideCallingUI();
            // Caller হিসেবে call window open করো
            await openCallWindow(data.callId, true, isVideo, false);
        });

        connection.on("CallReady", async (data) => {
            console.log("CallReady:", data);
            currentCallId = data.callId;
            isVideo = data.isVideo;
            // Receiver হিসেবে call window open করো
            await openCallWindow(data.callId, false, data.isVideo, false);
        });

        connection.on("CallRejected", (data) => {
            hideCallingUI();
            showToast("Call was rejected.", "warning");
        });

        connection.on("CallFailed", (reason) => {
            console.log("CallFailed:", reason);
            hideCallingUI();
            showToast(reason, "danger");
        });

        connection.on("CallEnded", (data) => {
            console.log("CallEnded:", data);
            endCallUI();
        });

        // ─── Group Call ───────────────────────────────
        connection.on("GroupCallInitiated", async (data) => {
            console.log("GroupCallInitiated:", data);
            currentCallId = data.callId;
            isVideo = data.isVideo;
            isGroup = true;
            await openCallWindow(data.callId, true, data.isVideo, true);
        });

        connection.on("IncomingGroupCall", (data) => {
            console.log("IncomingGroupCall:", data);
            showIncomingGroupCallUI(data);
        });

        connection.on("ExistingParticipants", async (data) => {
            console.log("ExistingParticipants:", data);
            currentCallId = data.callId;
            isVideo = data.isVideo;
            isGroup = true;

            for (const participantId of data.participants) {
                await createAndSendOffer(participantId, data.callId);
            }
        });

        connection.on("ParticipantJoined", async (data) => {
            console.log("ParticipantJoined:", data);
            addParticipantPlaceholder(data.userId, data.userName, data.userAvatar);
            await createAndSendOffer(data.userId, data.callId);
        });

        connection.on("ParticipantLeft", (data) => {
            removeParticipant(data.userId);
        });

        // ─── WebRTC Signaling ─────────────────────────
        connection.on("ReceiveOffer", async (data) => {
            console.log("ReceiveOffer from:", data.fromUserId);
            await handleOffer(data.fromUserId, data.callId, data.sdp);
        });

        connection.on("ReceiveAnswer", async (data) => {
            console.log("ReceiveAnswer from:", data.fromUserId);
            await handleAnswer(data.fromUserId, data.sdp);
        });

        connection.on("ReceiveIceCandidate", async (data) => {
            await handleIceCandidate(data.fromUserId, data.candidate);
        });

        connection.on("ParticipantMediaToggled", (data) => {
            updateParticipantMediaStatus(
                data.userId, data.audioEnabled, data.videoEnabled);
        });
    }

    // ════════════════════════════════════════════════
    //  CALL INITIATION
    // ════════════════════════════════════════════════

    async function startPrivateCall(receiverId, video) {
        if (!connection ||
            connection.state !== signalR.HubConnectionState.Connected) {
            showToast("Not connected. Please refresh.", "danger");
            return;
        }
        console.log("Starting call to:", receiverId, "video:", video);
        isVideo = video;
        isGroup = false;
        await connection.invoke("InitiateCall", receiverId, video);
    }

    async function startGroupCall(groupId, video) {
        if (!connection ||
            connection.state !== signalR.HubConnectionState.Connected) {
            showToast("Not connected. Please refresh.", "danger");
            return;
        }
        isVideo = video;
        isGroup = true;
        await connection.invoke("InitiateGroupCall", groupId, video);
    }

    async function acceptCall(callId, video) {
        currentCallId = callId;
        isVideo = video;
        hideIncomingCallUI();
        await connection.invoke("AcceptCall", callId);
    }

    async function rejectCall(callId) {
        hideIncomingCallUI();
        await connection.invoke("RejectCall", callId);
    }

    async function joinGroupCall(callId, video) {
        isVideo = video;
        hideIncomingCallUI();
        await connection.invoke("JoinGroupCall", callId);
    }

    // ════════════════════════════════════════════════
    //  LOCAL STREAM — mic/camera ছাড়াও চলবে
    // ════════════════════════════════════════════════

    async function startLocalStream(video) {
        try {
            // কী device আছে check করো
            const devices = await navigator.mediaDevices.enumerateDevices();
            const hasMic = devices.some(d => d.kind === "audioinput");
            const hasCamera = devices.some(d => d.kind === "videoinput");

            console.log("Devices — mic:", hasMic, "camera:", hasCamera);

            const constraints = {
                audio: hasMic,
                video: video && hasCamera
            };

            // দুটোই নেই
            if (!constraints.audio && !constraints.video) {
                console.warn("No media devices. Using empty stream.");
                localStream = new MediaStream();
                showToast(
                    "No microphone/camera detected. Joining without media.",
                    "warning"
                );
                return;
            }

            localStream = await navigator.mediaDevices.getUserMedia(constraints);

            const localVideo = document.getElementById("localVideo");
            if (localVideo && constraints.video) {
                localVideo.srcObject = localStream;
                localVideo.classList.remove("d-none");
            } else if (localVideo) {
                localVideo.classList.add("d-none");
            }

            console.log("✅ Local stream started.");

        } catch (err) {
            console.warn("Media error:", err.name, err.message);
            localStream = new MediaStream();

            if (err.name === "NotAllowedError" ||
                err.name === "PermissionDeniedError") {
                showToast("Permission denied for camera/microphone.", "warning");
            } else if (err.name === "NotFoundError") {
                showToast("No camera/microphone found.", "warning");
            } else if (err.name === "NotReadableError") {
                showToast("Camera/microphone is used by another app.", "warning");
            } else {
                showToast("Media unavailable. Joining without media.", "warning");
            }
        }
    }

    // ════════════════════════════════════════════════
    //  WebRTC CORE
    // ════════════════════════════════════════════════

    function createPeerConnection(targetUserId, callId) {
        // Already আছে কিনা check
        if (peerConnections[targetUserId]) {
            peerConnections[targetUserId].close();
        }

        const pc = new RTCPeerConnection(iceConfig);
        peerConnections[targetUserId] = pc;

        // Local tracks add করো (empty stream হলেও কোনো error নেই)
        if (localStream) {
            localStream.getTracks().forEach(track => {
                pc.addTrack(track, localStream);
            });
        }

        // Remote stream
        pc.ontrack = (event) => {
            console.log("Remote track received from:", targetUserId);
            if (event.streams && event.streams[0]) {
                addRemoteStream(targetUserId, event.streams[0]);
            }
        };

        // ICE Candidate
        pc.onicecandidate = (event) => {
            if (event.candidate) {
                connection.invoke(
                    "SendIceCandidate",
                    targetUserId,
                    callId,
                    JSON.stringify(event.candidate)
                ).catch(console.error);
            }
        };

        pc.oniceconnectionstatechange = () => {
            console.log(`ICE state (${targetUserId}): ${pc.iceConnectionState}`);
        };

        pc.onconnectionstatechange = () => {
            console.log(`Conn state (${targetUserId}): ${pc.connectionState}`);
            if (pc.connectionState === "failed" ||
                pc.connectionState === "disconnected") {
                removeParticipant(targetUserId);
            }
        };

        return pc;
    }

    async function createAndSendOffer(targetUserId, callId) {
        try {
            const pc = createPeerConnection(targetUserId, callId);

            const offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: isVideo
            });

            await pc.setLocalDescription(offer);

            await connection.invoke(
                "SendOffer",
                targetUserId,
                callId,
                JSON.stringify(offer)
            );

            console.log("✅ Offer sent to:", targetUserId);
        } catch (err) {
            console.error("createAndSendOffer error:", err);
        }
    }

    async function handleOffer(fromUserId, callId, sdpStr) {
        try {
            const pc = createPeerConnection(fromUserId, callId);
            const sdp = JSON.parse(sdpStr);

            await pc.setRemoteDescription(new RTCSessionDescription(sdp));

            const answer = await pc.createAnswer();
            await pc.setLocalDescription(answer);

            await connection.invoke(
                "SendAnswer",
                fromUserId,
                callId,
                JSON.stringify(answer)
            );

            console.log("✅ Answer sent to:", fromUserId);
        } catch (err) {
            console.error("handleOffer error:", err);
        }
    }

    async function handleAnswer(fromUserId, sdpStr) {
        try {
            const pc = peerConnections[fromUserId];
            if (!pc) {
                console.warn("No peer connection for:", fromUserId);
                return;
            }
            const sdp = JSON.parse(sdpStr);
            await pc.setRemoteDescription(new RTCSessionDescription(sdp));
            console.log("✅ Answer handled from:", fromUserId);
        } catch (err) {
            console.error("handleAnswer error:", err);
        }
    }

    async function handleIceCandidate(fromUserId, candidateStr) {
        try {
            const pc = peerConnections[fromUserId];
            if (!pc) return;

            const candidate = JSON.parse(candidateStr);
            await pc.addIceCandidate(new RTCIceCandidate(candidate));
        } catch (err) {
            console.error("ICE candidate error:", err);
        }
    }

    // ════════════════════════════════════════════════
    //  CALL WINDOW
    // ════════════════════════════════════════════════

    async function openCallWindow(callId, isCaller, video, group) {
        currentCallId = callId;
        isVideo = video;
        isGroup = group;

        // Local stream শুরু করো
        await startLocalStream(video);

        // Modal open করো
        const modalEl = document.getElementById("callModal");
        const modal = new bootstrap.Modal(modalEl, { backdrop: false });
        modal.show();

        // UI setup
        document.getElementById("callModalLabel").textContent =
            video ? "📹 Video Call" : "📞 Audio Call";

        const audioCallUI = document.getElementById("audioCallUI");
        const remoteVideosArea = document.getElementById("remoteVideosArea");
        const cameraBtnContainer = document.getElementById("cameraBtnContainer");
        const localVideoEl = document.getElementById("localVideo");

        if (video) {
            audioCallUI.classList.add("d-none");
            remoteVideosArea.classList.remove("d-none");
            if (cameraBtnContainer) cameraBtnContainer.classList.remove("d-none");
            if (localVideoEl) localVideoEl.classList.remove("d-none");
        } else {
            audioCallUI.classList.remove("d-none");
            remoteVideosArea.classList.add("d-none");
            if (cameraBtnContainer) cameraBtnContainer.classList.add("d-none");
            if (localVideoEl) localVideoEl.classList.add("d-none");
        }

        // Timer শুরু করো
        startCallTimer();

        // Private call এ caller হলে — receiver accept করার পরে offer পাঠাও
        // এটা CallAccepted event এ receiver এর userId দরকার
        // CallHub থেকে CallAccepted এ receiverId পাঠাচ্ছি না
        // তাই private call এ offer পাঠানো হবে
        // CallReady event এ receiver offer পাবে caller এর কাছ থেকে
        console.log("Call window opened. isCaller:", isCaller, "group:", group);
    }

    function endCallUI() {
        stopCallTimer();

        const modalEl = document.getElementById("callModal");
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();

        cleanupCall();
        showToast("Call ended.", "secondary");
    }

    async function hangUp() {
        if (!currentCallId) return;

        try {
            await connection.invoke("EndCall", currentCallId);
        } catch (err) {
            console.error("hangUp error:", err);
        }

        stopCallTimer();
        cleanupCall();

        const modalEl = document.getElementById("callModal");
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
    }

    function cleanupCall() {
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            localStream = null;
        }

        Object.values(peerConnections).forEach(pc => pc.close());
        peerConnections = {};

        currentCallId = null;
        audioEnabled = true;
        videoEnabled = true;

        const remoteArea = document.getElementById("remoteVideosArea");
        if (remoteArea) remoteArea.innerHTML = "";

        const localVideo = document.getElementById("localVideo");
        if (localVideo) localVideo.srcObject = null;

        const audioCallUI = document.getElementById("audioCallUI");
        if (audioCallUI) audioCallUI.classList.add("d-none");
    }

    // ════════════════════════════════════════════════
    //  CALL TIMER
    // ════════════════════════════════════════════════

    function startCallTimer() {
        stopCallTimer();
        callSeconds = 0;
        callTimer = setInterval(() => {
            callSeconds++;
            const mins = String(Math.floor(callSeconds / 60)).padStart(2, "0");
            const secs = String(callSeconds % 60).padStart(2, "0");
            const el = document.getElementById("callDuration");
            if (el) el.textContent = `${mins}:${secs}`;
        }, 1000);
    }

    function stopCallTimer() {
        if (callTimer) {
            clearInterval(callTimer);
            callTimer = null;
            callSeconds = 0;
        }
        const el = document.getElementById("callDuration");
        if (el) el.textContent = "00:00";
    }

    // ════════════════════════════════════════════════
    //  MEDIA CONTROLS
    // ════════════════════════════════════════════════

    async function toggleAudio() {
        audioEnabled = !audioEnabled;

        if (localStream) {
            localStream.getAudioTracks().forEach(t => {
                t.enabled = audioEnabled;
            });
        }

        const muteBtn = document.getElementById("muteBtn");
        if (muteBtn) {
            muteBtn.innerHTML = audioEnabled
                ? '<i class="bi bi-mic-fill"></i>'
                : '<i class="bi bi-mic-mute-fill text-danger"></i>';
        }

        if (currentCallId) {
            await connection.invoke("ToggleMedia",
                currentCallId, audioEnabled, videoEnabled);
        }
    }

    async function toggleVideo() {
        videoEnabled = !videoEnabled;

        if (localStream) {
            localStream.getVideoTracks().forEach(t => {
                t.enabled = videoEnabled;
            });
        }

        const cameraBtn = document.getElementById("cameraBtn");
        if (cameraBtn) {
            cameraBtn.innerHTML = videoEnabled
                ? '<i class="bi bi-camera-video-fill"></i>'
                : '<i class="bi bi-camera-video-off-fill text-danger"></i>';
        }

        if (currentCallId) {
            await connection.invoke("ToggleMedia",
                currentCallId, audioEnabled, videoEnabled);
        }
    }

    // ════════════════════════════════════════════════
    //  VIDEO/AUDIO DOM
    // ════════════════════════════════════════════════

    function addRemoteStream(userId, stream) {
        const area = document.getElementById("remoteVideosArea");
        if (!area) return;

        let container = document.getElementById(`remote-${userId}`);
        if (!container) {
            container = document.createElement("div");
            container.id = `remote-${userId}`;
            container.className = "remote-participant";
            area.appendChild(container);
        }

        // Placeholder সরাও, video দেখাও
        container.innerHTML = "";

        const video = document.createElement("video");
        video.id = `video-${userId}`;
        video.autoplay = true;
        video.playsInline = true;
        video.className = "remote-video";
        video.srcObject = stream;

        container.appendChild(video);
        console.log("✅ Remote stream added for:", userId);
    }

    function addParticipantPlaceholder(userId, userName, avatar) {
        const area = document.getElementById("remoteVideosArea");
        if (!area || document.getElementById(`remote-${userId}`)) return;

        const container = document.createElement("div");
        container.id = `remote-${userId}`;
        container.className = "remote-participant";
        container.innerHTML = `
            <div class="participant-placeholder">
                ${avatar
                ? `<img src="${avatar}" class="rounded-circle"
                            style="width:80px;height:80px;object-fit:cover;" />`
                : `<div class="rounded-circle bg-primary text-white
                               d-flex align-items-center justify-content-center"
                            style="width:80px;height:80px;font-size:2rem;">
                            ${userName[0]}
                        </div>`}
                <p class="mt-2 fw-semibold text-white">${userName}</p>
                <small class="text-muted">Connecting...</small>
            </div>`;

        area.appendChild(container);
    }

    function removeParticipant(userId) {
        const el = document.getElementById(`remote-${userId}`);
        if (el) el.remove();

        const pc = peerConnections[userId];
        if (pc) {
            pc.close();
            delete peerConnections[userId];
        }
    }

    function updateParticipantMediaStatus(userId, audio, video) {
        const videoEl = document.getElementById(`video-${userId}`);
        if (videoEl) {
            videoEl.style.opacity = video ? "1" : "0.3";
        }
    }

    // ════════════════════════════════════════════════
    //  UI HELPERS
    // ════════════════════════════════════════════════

    function showCallingUI() {
        const modalEl = document.getElementById("callingModal");
        if (!modalEl) return;
        new bootstrap.Modal(modalEl, { backdrop: "static" }).show();
    }

    function hideCallingUI() {
        const modalEl = document.getElementById("callingModal");
        if (!modalEl) return;
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
    }

    function showIncomingCallUI(data) {
        const nameEl = document.getElementById("incomingCallerName");
        const typeEl = document.getElementById("incomingCallType");
        const avatarEl = document.getElementById("incomingCallerAvatar");

        if (nameEl) nameEl.textContent = data.callerName;
        if (typeEl) typeEl.textContent =
            data.isVideo ? "📹 Incoming Video Call" : "📞 Incoming Audio Call";

        if (avatarEl) {
            avatarEl.innerHTML = data.callerAvatar
                ? `<img src="${data.callerAvatar}" class="rounded-circle"
                        style="width:70px;height:70px;object-fit:cover;" />`
                : `<div class="rounded-circle bg-primary text-white d-flex
                               align-items-center justify-content-center mx-auto"
                        style="width:70px;height:70px;font-size:2rem;">
                        ${data.callerName[0]}
                   </div>`;
        }

        const acceptBtn = document.getElementById("acceptCallBtn");
        const rejectBtn = document.getElementById("rejectCallBtn");

        if (acceptBtn) acceptBtn.onclick = () =>
            CallManager.acceptCall(data.callId, data.isVideo);
        if (rejectBtn) rejectBtn.onclick = () =>
            CallManager.rejectCall(data.callId);

        const modalEl = document.getElementById("incomingCallModal");
        if (modalEl) {
            new bootstrap.Modal(modalEl, { backdrop: "static" }).show();
        }
    }

    function showIncomingGroupCallUI(data) {
        const nameEl = document.getElementById("incomingCallerName");
        const typeEl = document.getElementById("incomingCallType");

        if (nameEl) nameEl.textContent =
            `${data.callerName} is calling in "${data.groupName}"`;
        if (typeEl) typeEl.textContent =
            data.isVideo ? "📹 Group Video Call" : "📞 Group Audio Call";

        const acceptBtn = document.getElementById("acceptCallBtn");
        const rejectBtn = document.getElementById("rejectCallBtn");

        if (acceptBtn) acceptBtn.onclick = () =>
            CallManager.joinGroupCall(data.callId, data.isVideo);
        if (rejectBtn) rejectBtn.onclick = () =>
            CallManager.rejectCall(data.callId);

        const modalEl = document.getElementById("incomingCallModal");
        if (modalEl) {
            new bootstrap.Modal(modalEl, { backdrop: "static" }).show();
        }
    }

    function hideIncomingCallUI() {
        const modalEl = document.getElementById("incomingCallModal");
        if (!modalEl) return;
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide();
    }

    async function cancelCall() {
        if (currentCallId) {
            try {
                await connection.invoke("EndCall", currentCallId);
            } catch (err) {
                console.error("cancelCall error:", err);
            }
        }
        hideCallingUI();
        currentCallId = null;
    }

    function showToast(message, type = "info") {
        const toast = document.getElementById("callToast");
        if (!toast) return;
        toast.querySelector(".toast-body").textContent = message;
        toast.className =
            `toast align-items-center text-bg-${type} border-0`;
        new bootstrap.Toast(toast, { delay: 4000 }).show();
    }

    // ─── Public API ───────────────────────────────────
    return {
        init,
        startPrivateCall,
        startGroupCall,
        acceptCall,
        rejectCall,
        joinGroupCall,
        hangUp,
        toggleAudio,
        toggleVideo,
        cancelCall
    };

})();