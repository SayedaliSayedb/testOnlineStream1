class WebRTCStreamer {
    constructor() {
        this.localStream = null;
        this.peerConnections = new Map(); // viewerId -> RTCPeerConnection
        this.isStreaming = false;
        this.streamId = null;
        this.connection = null;

        this.initializeElements();
        this.initializeSignalR();
        this.setupEventListeners();
    }

    initializeElements() {
        this.localVideo = document.getElementById('localVideo');
        this.startStreamBtn = document.getElementById('startStreamBtn');
        this.stopStreamBtn = document.getElementById('stopStreamBtn');
        this.streamTitleInput = document.getElementById('streamTitle');
        this.viewerCount = document.getElementById('viewerCount');
        this.viewersList = document.getElementById('viewersList');
        this.streamStatus = document.getElementById('streamStatus');
    }

    async initializeSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/webrtcHub")
            .withAutomaticReconnect()
            .build();

        try {
            await this.connection.start();
            console.log("✅ Streamer connected to WebRTC Hub");

            this.setupHubListeners();

        } catch (err) {
            console.error("❌ Streamer connection error:", err);
            this.showNotification("خطا در اتصال به سرور", "error");
        }
    }

    setupHubListeners() {
        this.connection.on("StreamStarted", (streamId) => {
            console.log("🎬 Stream started with ID:", streamId);
            this.streamId = streamId;
            this.isStreaming = true;
            this.updateUI(true);
            this.showNotification("پخش زنده شما شروع شد", "success");
        });

        this.connection.on("ViewerJoined", (viewer) => {
            console.log("👤 Viewer joined:", viewer.name);
            this.addViewerToList(viewer);
            this.updateViewerCount();
        });

        this.connection.on("ViewerLeft", (viewerId) => {
            console.log("👤 Viewer left:", viewerId);
            this.removeViewerFromList(viewerId);
            this.removePeerConnection(viewerId);
            this.updateViewerCount();
        });

        this.connection.on("StreamStatsUpdated", (stats) => {
            this.viewerCount.textContent = stats.viewerCount;
        });

        this.connection.on("ReceiveSignal", async (signal) => {
            await this.handleSignal(signal);
        });
    }

    setupEventListeners() {
        this.startStreamBtn.addEventListener('click', () => this.startStreaming());
        this.stopStreamBtn.addEventListener('click', () => this.stopStreaming());
    }

    async startStreaming() {
        try {
            console.log("🎬 Starting WebRTC stream...");

            const title = this.streamTitleInput?.value || "پخش زنده";

            // دریافت دسترسی به دوربین و میکروفون
            this.localStream = await navigator.mediaDevices.getUserMedia({
                video: {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    frameRate: { ideal: 30 }
                },
                audio: true
            });

            this.localVideo.srcObject = this.localStream;

            // شروع استریم در سرور
            await this.connection.invoke("StartStreaming", title);

        } catch (error) {
            console.error("❌ Error starting stream:", error);

            let errorMessage = "خطا در شروع پخش زنده";
            if (error.name === 'NotAllowedError') {
                errorMessage = "دسترسی به دوربین/میکروفون رد شد";
            } else if (error.name === 'NotFoundError') {
                errorMessage = "دوربین پیدا نشد";
            } else if (error.name === 'NotSupportedError') {
                errorMessage = "مرورگر شما از WebRTC پشتیبانی نمی‌کند";
            }

            this.showNotification(errorMessage, "error");
        }
    }

    async stopStreaming() {
        try {
            await this.connection.invoke("StopStreaming");

            // قطع تمام اتصالات Peer
            this.peerConnections.forEach((pc, viewerId) => {
                pc.close();
            });
            this.peerConnections.clear();

            if (this.localStream) {
                this.localStream.getTracks().forEach(track => track.stop());
                this.localStream = null;
            }

            this.isStreaming = false;
            this.streamId = null;
            this.updateUI(false);
            this.clearViewersList();

            this.showNotification("پخش زنده متوقف شد", "info");

        } catch (error) {
            console.error("❌ Error stopping stream:", error);
            this.showNotification("خطا در توقف پخش زنده", "error");
        }
    }

    async createPeerConnection(viewerId) {
        const configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                { urls: 'stun:stun2.l.google.com:19302' }
            ]
        };

        const peerConnection = new RTCPeerConnection(configuration);

        // اضافه کردن trackهای محلی به اتصال
        this.localStream.getTracks().forEach(track => {
            peerConnection.addTrack(track, this.localStream);
        });

        // مدیریت ICE candidates
        peerConnection.onicecandidate = (event) => {
            if (event.candidate) {
                this.connection.invoke("SendSignal", viewerId, "ice-candidate", JSON.stringify(event.candidate));
            }
        };

        // مدیریت تغییرات وضعیت اتصال
        peerConnection.onconnectionstatechange = () => {
            console.log(`Connection state with ${viewerId}: ${peerConnection.connectionState}`);

            if (peerConnection.connectionState === 'connected') {
                console.log(`✅ Connected to viewer: ${viewerId}`);
            } else if (peerConnection.connectionState === 'failed' || peerConnection.connectionState === 'disconnected') {
                console.log(`❌ Connection failed with viewer: ${viewerId}`);
                this.removePeerConnection(viewerId);
            }
        };

        this.peerConnections.set(viewerId, peerConnection);
        return peerConnection;
    }

    async handleSignal(signal) {
        try {
            const { senderConnectionId, type, data } = signal;

            let peerConnection = this.peerConnections.get(senderConnectionId);

            if (!peerConnection && type === 'offer') {
                peerConnection = await this.createPeerConnection(senderConnectionId);
            }

            if (!peerConnection) return;

            switch (type) {
                case 'offer':
                    await peerConnection.setRemoteDescription(JSON.parse(data));
                    const answer = await peerConnection.createAnswer();
                    await peerConnection.setLocalDescription(answer);
                    await this.connection.invoke("SendSignal", senderConnectionId, "answer", JSON.stringify(answer));
                    break;

                case 'ice-candidate':
                    await peerConnection.addIceCandidate(JSON.parse(data));
                    break;
            }
        } catch (error) {
            console.error("❌ Error handling signal:", error);
        }
    }

    removePeerConnection(viewerId) {
        const pc = this.peerConnections.get(viewerId);
        if (pc) {
            pc.close();
            this.peerConnections.delete(viewerId);
        }
    }

    addViewerToList(viewer) {
        const viewerElement = document.createElement('div');
        viewerElement.className = 'list-group-item d-flex justify-content-between align-items-center';
        viewerElement.id = `viewer-${viewer.connectionId}`;
        viewerElement.innerHTML = `
            <div>
                <strong>${viewer.name}</strong>
                <small class="text-muted d-block">${new Date(viewer.joinTime).toLocaleTimeString('fa-IR')}</small>
            </div>
            <span class="badge bg-success">آنلاین</span>
        `;

        this.viewersList.appendChild(viewerElement);
    }

    removeViewerFromList(viewerId) {
        const element = document.getElementById(`viewer-${viewerId}`);
        if (element) {
            element.remove();
        }
    }

    clearViewersList() {
        this.viewersList.innerHTML = '';
        this.viewerCount.textContent = '0';
    }

    updateViewerCount() {
        const count = this.viewersList.children.length;
        this.viewerCount.textContent = count;
    }

    updateUI(streaming) {
        this.startStreamBtn.disabled = streaming;
        this.stopStreamBtn.disabled = !streaming;

        if (this.streamTitleInput) {
            this.streamTitleInput.disabled = streaming;
        }

        if (streaming) {
            this.streamStatus.innerHTML = '<span class="live-indicator"></span>در حال پخش زنده';
            this.streamStatus.className = 'badge bg-danger';
        } else {
            this.streamStatus.textContent = 'آفلاین';
            this.streamStatus.className = 'badge bg-secondary';
        }
    }

    showNotification(message, type = 'info') {
        const alertClass = {
            'success': 'alert-success',
            'error': 'alert-danger',
            'warning': 'alert-warning',
            'info': 'alert-info'
        }[type] || 'alert-info';

        const alertDiv = document.createElement('div');
        alertDiv.className = `alert ${alertClass} alert-dismissible fade show position-fixed`;
        alertDiv.style.cssText = 'top: 20px; left: 20px; z-index: 1050; min-width: 300px;';
        alertDiv.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(alertDiv);

        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }
}

// راه‌اندازی استریمر
document.addEventListener('DOMContentLoaded', () => {
    window.streamer = new WebRTCStreamer();
});