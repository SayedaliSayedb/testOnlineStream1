class FullScreenViewer {
    constructor() {
        this.activeStreams = new Map();
        this.currentMainStream = null;
        this.connection = null;
        this.isStreamsListVisible = false;

        this.initialize();
    }

    async initialize() {
        this.initializeElements();
        await this.initializeSignalR();
        this.setupEventListeners();

        console.log("🚀 FullScreen Viewer initialized");
    }

    initializeElements() {
        this.mainVideo = document.getElementById('mainVideo');
        this.streamsGrid = document.getElementById('streamsGrid');
        this.streamsListContainer = document.getElementById('streamsListContainer');
        this.streamsCount = document.getElementById('streamsCount');
        this.connectionStatus = document.getElementById('connectionStatus');
        this.emptyState = document.getElementById('emptyState');
        this.toggleStreamsList = document.getElementById('toggleStreamsList');
        this.refreshStreams = document.getElementById('refreshStreams');
        this.retryButton = document.getElementById('retryButton');

        console.log("✅ Elements initialized");
    }

    async initializeSignalR() {
        try {
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/webrtcHub")
                .withAutomaticReconnect()
                .build();

            this.setupHubListeners();

            await this.connection.start();
            console.log("✅ SignalR connection established");
            this.updateConnectionStatus('connected', 'اتصالمون وصله');

            // درخواست لیست استریم‌ها پس از اتصال
            await this.requestStreamList();

        } catch (error) {
            console.error("❌ SignalR connection failed:", error);
            this.updateConnectionStatus('error', 'قطع شد!');
        }
    }

    setupHubListeners() {
        // رویداد دریافت لیست استریم‌ها
        this.connection.on("StreamListUpdated", (streams) => {
            console.log("📋 Received stream list:", streams);
            this.handleStreamListUpdate(streams);
        });

        // رویداد عضویت در استریم
        this.connection.on("JoinedStream", (streamInfo) => {
            console.log("✅ Joined stream:", streamInfo);
            this.handleJoinedStream(streamInfo);
        });

        // رویداد پایان استریم
        this.connection.on("StreamEnded", (streamId) => {
            console.log("⏹ Stream ended:", streamId);
            this.removeStream(streamId);
        });

        // رویدادهای WebRTC
        this.connection.on("ReceiveSignal", (signal) => {
            this.handleSignal(signal);
        });

        // مدیریت اتصال مجدد
        this.connection.onreconnecting(() => {
            console.log("🔄 Reconnecting...");
            this.updateConnectionStatus('reconnecting', 'در حال اتصال مجدد...');
        });

        this.connection.onreconnected(() => {
            console.log("✅ Reconnected");
            this.updateConnectionStatus('connected', 'اتصالمون وصله');
            this.requestStreamList();
        });

        this.connection.onclose(() => {
            console.log("❌ Connection closed");
            this.updateConnectionStatus('error', 'اتصال قطع شد');
        });
    }

    setupEventListeners() {
        this.refreshStreams?.addEventListener('click', () => this.requestStreamList());
        this.retryButton?.addEventListener('click', () => this.requestStreamList());
        this.toggleStreamsList?.addEventListener('click', () => this.toggleStreamsListVisibility());

        // کلیدهای صفحه‌کلید
        document.addEventListener('keydown', (e) => {
            switch (e.key) {
                case ' ':
                    e.preventDefault();
                    this.toggleStreamsListVisibility();
                    break;
                case 'Escape':
                    this.exitViewerMode();
                    break;
            }
        });
    }

    async requestStreamList() {
        try {
            if (this.connection?.state === 'Connected') {
                await this.connection.invoke("RequestStreamList");
                console.log("📨 Requested stream list from server");
            } else {
                console.warn("⚠️ Cannot request stream list - connection not ready");
            }
        } catch (error) {
            console.error("❌ Error requesting stream list:", error);
        }
    }

    handleStreamListUpdate(streams) {
        if (!streams || streams.length === 0) {
            this.showEmptyState();
            return;
        }

        this.hideEmptyState();
        console.log(`🔄 Processing ${streams.length} stream(s)`);

        // پاکسازی استریم‌های قدیمی که دیگر فعال نیستند
        this.cleanupInactiveStreams(streams);

        // اضافه کردن استریم‌های جدید
        streams.forEach(stream => {
            if (!this.activeStreams.has(stream.streamId)) {
                this.addStream(stream);
            }
        });

        this.updateStreamsCount();
        this.updateStreamsListVisibility();
    }

    addStream(streamInfo) {
        console.log("➕ Adding stream:", streamInfo.streamId);

        // ایجاد المان استریم
        const streamElement = this.createStreamElement(streamInfo);
        this.streamsGrid.appendChild(streamElement);

        // ذخیره اطلاعات استریم
        this.activeStreams.set(streamInfo.streamId, {
            element: streamElement,
            peerConnection: null,
            stream: null,
            info: streamInfo,
            isConnected: false
        });

        // اگر اولین استریم است، آن را به عنوان اصلی تنظیم و متصل شو
        if (!this.currentMainStream) {
            console.log("🎯 Setting as main stream:", streamInfo.streamId);
            this.setMainStream(streamInfo.streamId);
        }

        // اتصال به استریم
        this.connectToStream(streamInfo);
    }

    createStreamElement(streamInfo) {
        const streamElement = document.createElement('div');
        streamElement.className = 'stream-thumbnail';
        streamElement.id = `stream-${streamInfo.streamId}`;

        streamElement.innerHTML = `
            <div class="thumbnail-container">
                <div class="thumbnail-placeholder">
                    <i class="bi bi-camera-video"></i>
                    <div class="loading-text">در حال اتصال...</div>
                </div>
            </div>
            <div class="stream-info">
                <div class="stream-title">${this.escapeHtml(streamInfo.title)}</div>
                <div class="stream-stats">
                    <span class="viewer-count">${streamInfo.viewerCount} بیننده</span>
                    <span class="live-indicator">● زنده</span>
                </div>
            </div>
        `;

        // کلیک برای انتخاب به عنوان استریم اصلی
        streamElement.addEventListener('click', () => {
            this.setMainStream(streamInfo.streamId);
        });

        return streamElement;
    }

    async connectToStream(streamInfo) {
        try {
            console.log(`🔗 Connecting to stream: ${streamInfo.streamId}`);
            await this.connection.invoke("JoinStream", streamInfo.streamId, "بیننده تمام‌صفحه");
        } catch (error) {
            console.error(`❌ Error joining stream ${streamInfo.streamId}:`, error);
        }
    }

    handleJoinedStream(streamInfo) {
        console.log(`🎬 Starting WebRTC for stream: ${streamInfo.streamId}`);
        this.initiateWebRTCConnection(streamInfo.streamId, streamInfo.streamerConnectionId);
    }

    async initiateWebRTCConnection(streamId, streamerConnectionId) {
        try {
            const configuration = {
                iceServers: [
                    { urls: 'stun:stun.l.google.com:19302' },
                    { urls: 'stun:stun1.l.google.com:19302' }
                ]
            };

            const peerConnection = new RTCPeerConnection(configuration);

            // مدیریت trackهای دریافتی
            peerConnection.ontrack = (event) => {
                console.log(`🎬 Received track for stream: ${streamId}`, event.track.kind);

                if (event.streams && event.streams[0]) {
                    const remoteStream = event.streams[0];
                    this.handleStreamReceived(streamId, remoteStream);
                }
            };

            // مدیریت ICE candidates
            peerConnection.onicecandidate = (event) => {
                if (event.candidate) {
                    this.connection.invoke("SendSignal", streamerConnectionId, "ice-candidate", JSON.stringify(event.candidate));
                }
            };

            // مدیریت وضعیت اتصال
            peerConnection.onconnectionstatechange = () => {
                console.log(`🔗 Connection state for ${streamId}:`, peerConnection.connectionState);
            };

            // ایجاد و ارسال offer
            const offer = await peerConnection.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });

            await peerConnection.setLocalDescription(offer);
            await this.connection.invoke("SendSignal", streamerConnectionId, "offer", JSON.stringify(offer));

            // ذخیره اتصال
            const streamData = this.activeStreams.get(streamId);
            if (streamData) {
                streamData.peerConnection = peerConnection;
                streamData.isConnected = true;
            }

            console.log(`✅ WebRTC initiated for: ${streamId}`);

        } catch (error) {
            console.error(`❌ WebRTC initiation failed for ${streamId}:`, error);
        }
    }

    handleStreamReceived(streamId, remoteStream) {
        const streamData = this.activeStreams.get(streamId);
        if (!streamData) return;

        streamData.stream = remoteStream;

        // به روزرسانی thumbnail
        this.updateThumbnailVideo(streamId, remoteStream);

        // اگر این استریم اصلی است، ویدیوی اصلی را تنظیم کن
        if (streamId === this.currentMainStream) {
            this.setMainVideoStream(remoteStream);
        }

        console.log(`✅ Stream ready: ${streamId}`);
    }

    updateThumbnailVideo(streamId, remoteStream) {
        const streamData = this.activeStreams.get(streamId);
        if (!streamData) return;

        const container = streamData.element.querySelector('.thumbnail-container');
        const placeholder = streamData.element.querySelector('.thumbnail-placeholder');

        if (placeholder && container) {
            // حذف placeholder و اضافه کردن ویدیو
            placeholder.remove();

            const video = document.createElement('video');
            video.className = 'thumbnail-video';
            video.autoplay = true;
            video.playsInline = true;
            video.muted = true;
            video.srcObject = remoteStream;

            video.onloadedmetadata = () => {
                console.log(`✅ Thumbnail metadata loaded: ${streamId}`);
            };

            container.appendChild(video);
        }
    }

    setMainVideoStream(remoteStream) {
        if (!this.mainVideo) return;

        this.mainVideo.srcObject = remoteStream;

        // تلاش برای پخش ویدیو
        const playVideo = () => {
            this.mainVideo.play().catch(error => {
                console.log("⏳ Waiting for video to be ready...");
                setTimeout(playVideo, 500);
            });
        };

        playVideo();

        console.log("✅ Main video stream set");
    }

    setMainStream(streamId) {
        if (!this.activeStreams.has(streamId)) {
            console.error("❌ Stream not found:", streamId);
            return;
        }

        console.log(`🎯 Setting main stream to: ${streamId}`);

        // حذف انتخاب از استریم قبلی
        if (this.currentMainStream) {
            const previousStream = this.activeStreams.get(this.currentMainStream);
            if (previousStream) {
                previousStream.element.classList.remove('active');
            }
        }

        // تنظیم استریم جدید
        this.currentMainStream = streamId;
        const currentStream = this.activeStreams.get(streamId);
        currentStream.element.classList.add('active');

        // تنظیم ویدیوی اصلی اگر داده موجود است
        if (currentStream.stream) {
            this.setMainVideoStream(currentStream.stream);
        } else {
            console.log("⏳ Waiting for stream data...");
            this.mainVideo.srcObject = null;
        }
    }

    handleSignal(signal) {
        try {
            const { senderConnectionId, type, data } = signal;

            // پیدا کردن استریم بر اساس streamerConnectionId
            let targetStreamId = null;
            for (let [streamId, streamData] of this.activeStreams) {
                if (streamData.info.streamerConnectionId === senderConnectionId) {
                    targetStreamId = streamId;
                    break;
                }
            }

            if (!targetStreamId) {
                console.log("❌ No stream found for signal from:", senderConnectionId);
                return;
            }

            const streamData = this.activeStreams.get(targetStreamId);
            if (!streamData?.peerConnection) {
                console.log("⚠️ No peer connection for stream:", targetStreamId);
                return;
            }

            const peerConnection = streamData.peerConnection;

            switch (type) {
                case 'answer':
                    console.log(`📥 Received answer for: ${targetStreamId}`);
                    const answer = JSON.parse(data);
                    peerConnection.setRemoteDescription(answer);
                    break;

                case 'ice-candidate':
                    const candidate = JSON.parse(data);
                    peerConnection.addIceCandidate(candidate);
                    break;
            }

        } catch (error) {
            console.error("❌ Error handling signal:", error);
        }
    }

    cleanupInactiveStreams(activeStreams) {
        const activeStreamIds = activeStreams.map(s => s.streamId);
        const streamsToRemove = [];

        for (let streamId of this.activeStreams.keys()) {
            if (!activeStreamIds.includes(streamId)) {
                streamsToRemove.push(streamId);
            }
        }

        streamsToRemove.forEach(streamId => this.removeStream(streamId));
    }

    removeStream(streamId) {
        const streamData = this.activeStreams.get(streamId);
        if (streamData) {
            if (streamData.peerConnection) {
                streamData.peerConnection.close();
            }
            if (streamData.element) {
                streamData.element.remove();
            }
            this.activeStreams.delete(streamId);

            // اگر استریم حذف شده اصلی بود، استریم دیگری را انتخاب کن
            if (this.currentMainStream === streamId) {
                this.currentMainStream = null;
                if (this.activeStreams.size > 0) {
                    const firstStreamId = Array.from(this.activeStreams.keys())[0];
                    this.setMainStream(firstStreamId);
                } else {
                    this.showEmptyState();
                }
            }

            this.updateStreamsCount();
            this.updateStreamsListVisibility();

            console.log(`🗑️ Removed stream: ${streamId}`);
        }
    }

    // متدهای کمکی
    showEmptyState() {
        if (this.emptyState) this.emptyState.style.display = 'flex';
        if (this.streamsListContainer) this.streamsListContainer.style.display = 'none';
    }

    hideEmptyState() {
        if (this.emptyState) this.emptyState.style.display = 'none';
        if (this.streamsListContainer) this.streamsListContainer.style.display = 'block';
    }

    updateStreamsCount() {
        if (this.streamsCount) {
            this.streamsCount.textContent = this.activeStreams.size;
        }
    }

    updateStreamsListVisibility() {
        if (!this.streamsListContainer) return;

        if (this.activeStreams.size <= 1) {
            this.streamsListContainer.style.display = 'none';
            this.isStreamsListVisible = false;
        } else {
            this.streamsListContainer.style.display = 'block';
        }
    }

    toggleStreamsListVisibility() {
        if (!this.streamsListContainer) return;

        this.isStreamsListVisible = !this.isStreamsListVisible;
        if (this.isStreamsListVisible) {
            this.streamsListContainer.classList.remove('collapsed');
        } else {
            this.streamsListContainer.classList.add('collapsed');
        }
    }

    updateConnectionStatus(status, text) {
        if (this.connectionStatus) {
            this.connectionStatus.textContent = text;
            this.connectionStatus.className = `status-${status}`;
        }
    }

    exitViewerMode() {
        window.location.href = '/';
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }
}

// راه‌اندازی
document.addEventListener('DOMContentLoaded', () => {
    console.log("🎬 Starting FullScreen Viewer...");
    window.fullScreenViewer = new FullScreenViewer();
});