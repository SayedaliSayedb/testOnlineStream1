class LiveStream {
    constructor() {
        this.localStream = null;
        this.isStreaming = false;
        this.connection = null;
        this.streamInterval = null;
        this.lastFrameTime = 0;
        this.frameRate = 10; // کاهش فریم ریت برای عملکرد بهتر

        this.initializeElements();
        this.initializeSignalR();
        this.setupEventListeners();
    }

    initializeElements() {
        this.localVideo = document.getElementById('localVideo');
        this.remoteVideo = document.getElementById('remoteVideo');
        this.startStreamBtn = document.getElementById('startStreamBtn');
        this.stopStreamBtn = document.getElementById('stopStreamBtn');
        this.viewerCount = document.getElementById('viewerCount');
        this.streamStatus = document.getElementById('streamStatus');
        this.enableAudio = document.getElementById('enableAudio');
        this.enableVideo = document.getElementById('enableVideo');
    }

    async initializeSignalR() {
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/streamHub")
            .withAutomaticReconnect()
            .build();

        try {
            await this.connection.start();
            console.log("✅ SignalR Connected");

            this.setupHubListeners();

            // دریافت وضعیت فعلی استریم
            const status = await this.connection.invoke("GetStreamStatus");
            this.updateStreamStatus(status);

        } catch (err) {
            console.error("❌ SignalR Connection Error:", err);
            setTimeout(() => this.initializeSignalR(), 5000);
        }
    }

    setupHubListeners() {
        this.connection.on("ReceiveStreamData", (data) => {
            console.log("📹 Received stream data");
            this.displayRemoteStream(data);
        });

        this.connection.on("ViewerCountUpdated", (count) => {
            console.log(`👥 Viewer count updated: ${count}`);
            this.viewerCount.textContent = count;
        });

        this.connection.on("StreamStarted", () => {
            console.log("🎬 Stream started received");
            this.updateStreamStatus("live");
            this.showNotification("پخش زنده شروع شد", "success");
        });

        this.connection.on("StreamEnded", () => {
            console.log("⏹ Stream ended received");
            this.updateStreamStatus("offline");
            this.remoteVideo.srcObject = null;
            this.showNotification("پخش زنده پایان یافت", "warning");
        });

        this.connection.onreconnecting(() => {
            console.log("🔌 Reconnecting...");
            this.showNotification("اتصال در حال برقراری مجدد...", "warning");
        });

        this.connection.onreconnected(() => {
            console.log("✅ Reconnected");
            this.showNotification("اتصال مجدد برقرار شد", "success");
        });
    }

    setupEventListeners() {
        this.startStreamBtn.addEventListener('click', () => this.startStream());
        this.stopStreamBtn.addEventListener('click', () => this.stopStream());

        this.enableAudio.addEventListener('change', () => this.toggleAudio());
        this.enableVideo.addEventListener('change', () => this.toggleVideo());
    }

    async startStream() {
        try {
            console.log("🎬 Starting stream...");

            // دریافت دسترسی به دوربین و میکروفون
            const constraints = {
                video: {
                    width: { ideal: 640 },  // کاهش رزولوشن
                    height: { ideal: 480 },
                    frameRate: { ideal: 15 } // کاهش فریم ریت
                },
                audio: this.enableAudio.checked
            };

            this.localStream = await navigator.mediaDevices.getUserMedia(constraints);
            console.log("✅ Camera access granted");

            this.localVideo.srcObject = this.localStream;

            // منتظر بمان تا ویدیو لود شود
            await new Promise((resolve) => {
                this.localVideo.onloadedmetadata = () => {
                    this.localVideo.play();
                    resolve();
                };
            });

            // ثبت به عنوان استریمر
            await this.connection.invoke("RegisterAsStreamer", "main");
            console.log("✅ Registered as streamer");

            this.isStreaming = true;
            this.updateUI(true);
            this.updateStreamStatus("live");

            // شروع ارسال داده‌های استریم
            this.startStreamingData();

            this.showNotification("پخش زنده با موفقیت شروع شد", "success");

        } catch (error) {
            console.error("❌ Error starting stream:", error);
            let errorMessage = "خطا در دسترسی به دوربین/میکروفون";

            if (error.name === 'NotAllowedError') {
                errorMessage = "دسترسی به دوربین رد شد. لطفاً مجوزها را بررسی کنید.";
            } else if (error.name === 'NotFoundError') {
                errorMessage = "دوربین پیدا نشد.";
            } else if (error.name === 'NotSupportedError') {
                errorMessage = "مرورگر شما از این قابلیت پشتیبانی نمی‌کند.";
            }

            this.showNotification(errorMessage, "error");
        }
    }

    stopStream() {
        console.log("⏹ Stopping stream...");

        if (this.localStream) {
            this.localStream.getTracks().forEach(track => {
                track.stop();
                console.log(`✅ Stopped track: ${track.kind}`);
            });
            this.localStream = null;
        }

        if (this.streamInterval) {
            clearInterval(this.streamInterval);
            this.streamInterval = null;
        }

        this.isStreaming = false;
        this.updateUI(false);
        this.updateStreamStatus("offline");

        this.showNotification("پخش زنده متوقف شد", "info");
    }

    startStreamingData() {
        console.log("📤 Starting to send stream data...");
        const canvas = document.createElement('canvas');
        const ctx = canvas.getContext('2d');
        const video = this.localVideo;

        this.streamInterval = setInterval(async () => {
            if (video.videoWidth > 0 && video.videoHeight > 0 && this.isStreaming) {
                try {
                    canvas.width = video.videoWidth;
                    canvas.height = video.videoHeight;

                    // رسم فریم فعلی روی canvas
                    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

                    // تبدیل به فرمت JPEG با کیفیت پایین برای کاهش حجم
                    const imageData = canvas.toDataURL('image/jpeg', 0.6);

                    // ارسال داده به سرور
                    await this.connection.invoke("SendStreamData", imageData);

                } catch (error) {
                    console.error("❌ Error sending stream data:", error);
                }
            }
        }, 1000 / this.frameRate); // ارسال با فریم ریت مشخص
    }

    displayRemoteStream(data) {
        try {
            // ایجاد یک تصویر جدید برای هر فریم
            const img = new Image();
            img.onload = () => {
                // اگر canvas نداریم، ایجاد کن
                let canvas = this.remoteVideo;
                if (!canvas || canvas.tagName !== 'CANVAS') {
                    // اگر remoteVideo یک ویدیو المنت است، آن را با canvas جایگزین کن
                    const videoElement = this.remoteVideo;
                    const parent = videoElement.parentElement;

                    canvas = document.createElement('canvas');
                    canvas.className = videoElement.className;
                    canvas.style.cssText = videoElement.style.cssText;
                    canvas.id = 'remoteCanvas';

                    parent.replaceChild(canvas, videoElement);
                    this.remoteVideo = canvas;
                }

                const ctx = canvas.getContext('2d');
                canvas.width = img.width;
                canvas.height = img.height;
                ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
            };
            img.src = data;
        } catch (error) {
            console.error("❌ Error displaying remote stream:", error);
        }
    }

    updateUI(streaming) {
        this.startStreamBtn.disabled = streaming;
        this.stopStreamBtn.disabled = !streaming;

        if (streaming) {
            this.startStreamBtn.classList.remove('btn-success');
            this.startStreamBtn.classList.add('btn-secondary');
            this.stopStreamBtn.classList.remove('btn-secondary');
            this.stopStreamBtn.classList.add('btn-danger');
        } else {
            this.startStreamBtn.classList.remove('btn-secondary');
            this.startStreamBtn.classList.add('btn-success');
            this.stopStreamBtn.classList.remove('btn-danger');
            this.stopStreamBtn.classList.add('btn-secondary');
        }
    }

    updateStreamStatus(status) {
        const statusText = status === 'live' ? 'زنده' : 'آفلاین';
        const statusClass = status === 'live' ? 'success' : 'secondary';

        this.streamStatus.textContent = statusText;
        this.streamStatus.className = `badge bg-${statusClass}`;

        if (status === 'live') {
            this.streamStatus.innerHTML = '<span class="live-indicator"></span>زنده';
        }
    }

    toggleAudio() {
        if (this.localStream) {
            const audioTracks = this.localStream.getAudioTracks();
            audioTracks.forEach(track => {
                track.enabled = this.enableAudio.checked;
            });
            console.log(`🔊 Audio ${this.enableAudio.checked ? 'enabled' : 'disabled'}`);
        }
    }

    toggleVideo() {
        if (this.localStream) {
            const videoTracks = this.localStream.getVideoTracks();
            videoTracks.forEach(track => {
                track.enabled = this.enableVideo.checked;
            });

            this.localVideo.style.opacity = this.enableVideo.checked ? '1' : '0.5';
            console.log(`📹 Video ${this.enableVideo.checked ? 'enabled' : 'disabled'}`);
        }
    }

    showNotification(message, type = 'info') {
        // ایجاد نوتیفیکیشن
        const alertClass = {
            'success': 'alert-success',
            'error': 'alert-danger',
            'warning': 'alert-warning',
            'info': 'alert-info'
        }[type] || 'alert-info';

        // اگر از قبل نوتیفیکیشن داریم، ابتدا آن را پاک کنیم
        const existingAlerts = document.querySelectorAll('.alert.position-fixed');
        existingAlerts.forEach(alert => alert.remove());

        const alertDiv = document.createElement('div');
        alertDiv.className = `alert ${alertClass} alert-dismissible fade show position-fixed`;
        alertDiv.style.cssText = 'top: 20px; right: 20px; z-index: 1050; min-width: 300px;';
        alertDiv.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(alertDiv);

        // حذف خودکار پس از 5 ثانیه
        setTimeout(() => {
            if (alertDiv.parentNode) {
                alertDiv.remove();
            }
        }, 5000);
    }
}

// راه‌اندازی هنگامی که DOM کاملاً لود شد
document.addEventListener('DOMContentLoaded', () => {
    console.log("🚀 Initializing Live Stream...");
    window.liveStream = new LiveStream();
});