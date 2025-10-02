class WebRTCViewer {
    constructor() {
        this.remoteStream = null;
        this.peerConnection = null;
        this.currentStreamId = null;
        this.connection = null;
        this.isWatching = false;
        this.streamerConnectionId = null;
        this.streamListRefreshInterval = null;

        this.initializeElements();
        this.initializeSignalR();
        this.setupEventListeners();
    }

    initializeElements() {
        this.remoteVideo = document.getElementById('remoteVideo');
        this.streamsList = document.getElementById('streamsList');
        this.watchStreamBtn = document.getElementById('watchStreamBtn');
        this.leaveStreamBtn = document.getElementById('leaveStreamBtn');
        this.viewerNameInput = document.getElementById('viewerName');
        this.streamInfo = document.getElementById('streamInfo');
        this.viewerCount = document.getElementById('viewerCount');
        this.streamStatus = document.getElementById('streamStatus');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.refreshStreamsBtn = document.getElementById('refreshStreamsBtn');
    }

    async initializeSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/webrtcHub")
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Debug)
            .build();

        try {
            await this.connection.start();
            console.log("✅ Viewer connected to WebRTC Hub");
            this.updateConnectionStatus('connected', 'متصل به سرور');

            this.setupHubListeners();
            this.startStreamListRefresher();

        } catch (err) {
            console.error("❌ Viewer connection error:", err);
            this.updateConnectionStatus('error', 'خطا در اتصال');
            this.showNotification("خطا در اتصال به سرور", "error");
        }
    }

    setupHubListeners() {
        this.connection.on("Connected", (connectionId) => {
            console.log("Connected with ID:", connectionId);
        });

        this.connection.on("StreamListUpdated", (streams) => {
            console.log("📋 Stream list updated. Total streams:", streams?.length || 0);
            this.updateStreamsList(streams);
        });

        this.connection.on("JoinedStream", (streamInfo) => {
            console.log("✅ Joined stream successfully:", streamInfo);
            this.currentStreamId = streamInfo.streamId;
            this.streamerConnectionId = streamInfo.streamerConnectionId;
            this.isWatching = true;
            this.updateUI(true);
            this.showStreamInfo(streamInfo);
            this.showNotification(`شما به پخش "${streamInfo.title}" پیوستید`, "success");

            // شروع فرآیند WebRTC
            this.initiateWebRTC();
        });

        this.connection.on("StreamEnded", () => {
            console.log("⏹ Stream ended by streamer");
            this.leaveStream();
            this.showNotification("پخش زنده توسط پخش کننده پایان یافت", "warning");
        });

        this.connection.on("StreamStatsUpdated", (stats) => {
            console.log("Viewer count updated:", stats.viewerCount);
            this.viewerCount.textContent = stats.viewerCount;
        });

        this.connection.on("ReceiveSignal", async (signal) => {
            console.log("📡 Received signal:", signal.type);
            await this.handleSignal(signal);
        });

        this.connection.on("Error", (message) => {
            console.error("Hub error:", message);
            this.showNotification(message, "error");
        });

        this.connection.onreconnecting(() => {
            this.updateConnectionStatus('reconnecting', 'در حال اتصال مجدد...');
        });

        this.connection.onreconnected(() => {
            this.updateConnectionStatus('connected', 'متصل به سرور');
            // پس از اتصال مجدد، لیست پخش‌ها را درخواست کن
            this.requestStreamList();
        });
    }

    setupEventListeners() {
        this.watchStreamBtn.addEventListener('click', () => this.watchSelectedStream());
        this.leaveStreamBtn.addEventListener('click', () => this.leaveStream());

        // اضافه کردن دکمه رفرش دستی
        if (this.refreshStreamsBtn) {
            this.refreshStreamsBtn.addEventListener('click', () => this.refreshStreamList());
        }
    }

    // شروع به روزرسانی دوره‌ای لیست پخش‌ها
    startStreamListRefresher() {
        // درخواست اولیه
        this.requestStreamList();

        // به روزرسانی دوره‌ای هر 10 ثانیه
        this.streamListRefreshInterval = setInterval(() => {
            this.requestStreamList();
        }, 10000);
    }

    // درخواست لیست پخش‌ها از سرور
    async requestStreamList() {
        try {
            if (this.connection && this.connection.state === 'Connected') {
                await this.connection.invoke("RequestStreamList");
            }
        } catch (error) {
            console.error("Error requesting stream list:", error);
        }
    }

    // رفرش دستی لیست
    async refreshStreamList() {
        try {
            this.showNotification("در حال به روزرسانی لیست پخش‌ها...", "info");
            await this.requestStreamList();

            // همچنین از API هم درخواست بده برای اطمینان
            await this.loadAvailableStreams();

        } catch (error) {
            console.error("Error refreshing stream list:", error);
        }
    }

    async loadAvailableStreams() {
        try {
            console.log("🔄 Loading available streams from API...");
            const response = await fetch('/api/stream/list');
            if (response.ok) {
                const streams = await response.json();
                console.log("Loaded streams from API:", streams);
                this.updateStreamsList(streams);
            } else {
                console.error("API response not OK:", response.status);
            }
        } catch (error) {
            console.error('Error loading streams from API:', error);
        }
    }

    updateStreamsList(streams) {
        const container = this.streamsList;

        if (!streams || streams.length === 0) {
            container.innerHTML = `
                <div class="text-center py-4">
                    <i class="bi bi-camera-video-off display-4 text-muted mb-3"></i>
                    <p class="text-muted">هیچ پخش زنده‌ای در حال حاضر فعال نیست</p>
                    <button class="btn btn-sm btn-outline-primary mt-2" onclick="viewer.refreshStreamList()">
                        <i class="bi bi-arrow-clockwise"></i> به روزرسانی
                    </button>
                </div>
            `;
            return;
        }

        // فیلتر کردن پخش جاری از لیست (اگر در حال تماشا هستیم)
        const filteredStreams = this.isWatching && this.currentStreamId
            ? streams.filter(stream => stream.streamId !== this.currentStreamId)
            : streams;

        const html = filteredStreams.map(stream => `
            <div class="col-md-6 col-lg-4 mb-4">
                <div class="card stream-card h-100">
                    <div class="card-body">
                        <div class="video-placeholder mb-3">
                            <i class="bi bi-camera-video display-4 text-muted"></i>
                        </div>
                        <h6 class="card-title">${this.escapeHtml(stream.title)}</h6>
                        <p class="card-text text-muted">
                            <small>شروع: ${new Date(stream.startTime).toLocaleString('fa-IR')}</small>
                        </p>
                        <div class="d-flex justify-content-between align-items-center">
                            <span class="badge bg-info">${stream.viewerCount} بیننده</span>
                            <button class="btn btn-success btn-sm" onclick="viewer.watchStream('${stream.streamId}')">
                                <i class="bi bi-play-fill"></i> مشاهده
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');

        container.innerHTML = html;

        // اگر در حال تماشا هستیم و پخش‌های دیگری وجود دارند، یک بخش جداگانه نشان بده
        if (this.isWatching && this.currentStreamId && filteredStreams.length > 0) {
            const currentStream = streams.find(s => s.streamId === this.currentStreamId);
            if (currentStream) {
                container.innerHTML = `
                    <div class="alert alert-info mb-3">
                        <i class="bi bi-info-circle"></i>
                        در حال مشاهده: <strong>${this.escapeHtml(currentStream.title)}</strong>
                    </div>
                    <h6 class="text-muted mb-3">پخش‌های زنده دیگر:</h6>
                    ${html}
                `;
            }
        }
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    async watchStream(streamId) {
        try {
            console.log("🎬 Attempting to watch stream:", streamId);
            this.updateConnectionStatus('connecting', 'در حال اتصال به پخش...');

            const viewerName = this.viewerNameInput?.value || "بیننده";
            await this.connection.invoke("JoinStream", streamId, viewerName);

        } catch (error) {
            console.error("❌ Error joining stream:", error);
            this.updateConnectionStatus('error', 'خطا در اتصال');
            this.showNotification("خطا در پیوستن به پخش زنده", "error");
        }
    }

    async watchSelectedStream() {
        const selectedStream = document.querySelector('input[name="streamSelect"]:checked');
        if (selectedStream) {
            await this.watchStream(selectedStream.value);
        } else {
            this.showNotification("لطفاً یک پخش را انتخاب کنید", "warning");
        }
    }

    async initiateWebRTC() {
        if (!this.streamerConnectionId) {
            console.error("❌ No streamer connection ID available");
            return;
        }

        try {
            console.log("🔄 Initiating WebRTC connection with streamer:", this.streamerConnectionId);

            await this.createPeerConnection();

            const offer = await this.peerConnection.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });

            await this.peerConnection.setLocalDescription(offer);

            console.log("📤 Sending offer to streamer");
            await this.connection.invoke("SendSignal", this.streamerConnectionId, "offer", JSON.stringify(offer));

        } catch (error) {
            console.error("❌ Error initiating WebRTC:", error);
            this.showNotification("خطا در برقراری ارتباط ویدیویی", "error");
        }
    }

    async createPeerConnection() {
        console.log("🔄 Creating peer connection...");

        const configuration = {
            iceServers: [
                { urls: 'stun:stun.l.google.com:19302' },
                { urls: 'stun:stun1.l.google.com:19302' },
                { urls: 'stun:stun2.l.google.com:19302' },
                { urls: 'stun:stun3.l.google.com:19302' },
                { urls: 'stun:stun4.l.google.com:19302' }
            ]
        };

        this.peerConnection = new RTCPeerConnection(configuration);

        this.peerConnection.ontrack = (event) => {
            console.log("🎬 Received remote track:", event.track.kind);
            if (event.streams && event.streams[0]) {
                this.remoteStream = event.streams[0];
                this.remoteVideo.srcObject = this.remoteStream;
                console.log("✅ Remote video stream set");

                this.remoteVideo.play().catch(e => {
                    console.error("❌ Error playing video:", e);
                });
            }
        };

        this.peerConnection.onicecandidate = (event) => {
            if (event.candidate && this.streamerConnectionId) {
                console.log("📤 Sending ICE candidate");
                this.connection.invoke("SendSignal", this.streamerConnectionId, "ice-candidate", JSON.stringify(event.candidate));
            }
        };

        this.peerConnection.onconnectionstatechange = () => {
            const state = this.peerConnection.connectionState;
            console.log("🔗 Peer connection state:", state);

            switch (state) {
                case 'connected':
                    this.updateConnectionStatus('connected', 'متصل به پخش');
                    this.streamStatus.innerHTML = '<span class="live-indicator"></span>در حال پخش';
                    this.streamStatus.className = 'badge bg-success';
                    break;
                case 'disconnected':
                case 'failed':
                    this.updateConnectionStatus('error', 'قطع ارتباط');
                    this.streamStatus.textContent = 'قطع ارتباط';
                    this.streamStatus.className = 'badge bg-danger';
                    break;
                case 'connecting':
                    this.updateConnectionStatus('connecting', 'در حال اتصال...');
                    this.streamStatus.textContent = 'در حال اتصال...';
                    this.streamStatus.className = 'badge bg-warning';
                    break;
            }
        };

        console.log("✅ Peer connection created successfully");
    }

    async handleSignal(signal) {
        try {
            const { senderConnectionId, type, data } = signal;
            console.log(`🔄 Handling signal type: ${type} from: ${senderConnectionId}`);

            if (!this.peerConnection) {
                console.error("❌ No peer connection available");
                return;
            }

            switch (type) {
                case 'answer':
                    console.log("📥 Received answer");
                    const answer = JSON.parse(data);
                    await this.peerConnection.setRemoteDescription(answer);
                    break;

                case 'ice-candidate':
                    console.log("📥 Received ICE candidate");
                    const candidate = JSON.parse(data);
                    await this.peerConnection.addIceCandidate(candidate);
                    break;

                default:
                    console.warn("Unknown signal type:", type);
            }
        } catch (error) {
            console.error("❌ Error handling signal:", error);
        }
    }

    async leaveStream() {
        console.log("🚪 Leaving stream...");

        if (this.currentStreamId) {
            try {
                await this.connection.invoke("LeaveStream", this.currentStreamId);
            } catch (error) {
                console.error("Error leaving stream:", error);
            }
        }

        if (this.peerConnection) {
            this.peerConnection.close();
            this.peerConnection = null;
        }

        if (this.remoteStream) {
            this.remoteStream.getTracks().forEach(track => track.stop());
            this.remoteStream = null;
        }

        this.remoteVideo.srcObject = null;
        this.currentStreamId = null;
        this.streamerConnectionId = null;
        this.isWatching = false;

        this.updateUI(false);
        this.clearStreamInfo();
        this.updateConnectionStatus('disconnected', 'قطع شده');

        // پس از ترک پخش، لیست را به روز کن
        this.requestStreamList();

        console.log("✅ Left stream successfully");
    }

    showStreamInfo(streamInfo) {
        this.streamInfo.innerHTML = `
            <div class="card">
                <div class="card-body">
                    <h5 class="card-title">${this.escapeHtml(streamInfo.title)}</h5>
                    <p class="card-text">
                        <strong>پخش کننده:</strong> ${streamInfo.streamerConnectionId.substring(0, 8)}...<br>
                        <strong>شروع پخش:</strong> ${new Date(streamInfo.startTime).toLocaleString('fa-IR')}<br>
                        <strong>تعداد بینندگان:</strong> <span id="currentViewerCount">${streamInfo.viewerCount}</span>
                    </p>
                    <div class="alert alert-success">
                        <i class="bi bi-info-circle"></i>
                        در حال برقراری ارتباط ویدیویی...
                    </div>
                </div>
            </div>
        `;
    }

    clearStreamInfo() {
        this.streamInfo.innerHTML = `
            <div class="text-center text-muted py-4">
                <i class="bi bi-camera-video-off display-4"></i>
                <p>هیچ پخشی در حال مشاهده نیست</p>
            </div>
        `;
    }

    updateUI(watching) {
        this.watchStreamBtn.disabled = watching;
        this.leaveStreamBtn.disabled = !watching;

        if (this.viewerNameInput) {
            this.viewerNameInput.disabled = watching;
        }

        if (watching) {
            this.streamStatus.textContent = 'در حال اتصال...';
            this.streamStatus.className = 'badge bg-warning';
        } else {
            this.streamStatus.textContent = 'آماده';
            this.streamStatus.className = 'badge bg-secondary';
        }
    }

    updateConnectionStatus(status, text) {
        if (this.connectionStatus) {
            this.connectionStatus.textContent = text;
            const statusClass = {
                'connected': 'text-success',
                'connecting': 'text-warning',
                'reconnecting': 'text-warning',
                'error': 'text-danger',
                'disconnected': 'text-muted'
            }[status] || 'text-muted';

            this.connectionStatus.className = statusClass;
        }
    }

    showNotification(message, type = 'info') {
        const alertClass = {
            'success': 'alert-success',
            'error': 'alert-danger',
            'warning': 'alert-warning',
            'info': 'alert-info'
        }[type] || 'alert-info';

        const existingAlerts = document.querySelectorAll('.alert.position-fixed');
        existingAlerts.forEach(alert => alert.remove());

        const alertDiv = document.createElement('div');
        alertDiv.className = `alert ${alertClass} alert-dismissible fade show position-fixed`;
        alertDiv.style.cssText = 'top: 20px; left: 20px; z-index: 1050; min-width: 300px;';
        alertDiv.innerHTML = `
            <div class="d-flex align-items-center">
                <i class="bi ${this.getNotificationIcon(type)} me-2"></i>
                <div>${message}</div>
            </div>
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(alertDiv);

        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }

    getNotificationIcon(type) {
        const icons = {
            'success': 'bi-check-circle-fill',
            'error': 'bi-exclamation-triangle-fill',
            'warning': 'bi-exclamation-circle-fill',
            'info': 'bi-info-circle-fill'
        };
        return icons[type] || 'bi-info-circle-fill';
    }

    // تمیز کردن منابع هنگام بسته شدن صفحه
    destroy() {
        if (this.streamListRefreshInterval) {
            clearInterval(this.streamListRefreshInterval);
        }
        if (this.connection) {
            this.connection.stop();
        }
    }
}

// راه‌اندازی بیننده و مدیریت رویداد unload
document.addEventListener('DOMContentLoaded', () => {
    console.log("🚀 Initializing WebRTC Viewer...");
    window.viewer = new WebRTCViewer();

    // تمیز کردن منابع هنگام بسته شدن صفحه
    window.addEventListener('beforeunload', () => {
        if (window.viewer) {
            window.viewer.destroy();
        }
    });
});