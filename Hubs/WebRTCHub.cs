using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WebApplication1.Models;

namespace WebApplication1.Hubs
{
    public class WebRTCHub : Hub
    {
        private static readonly ConcurrentDictionary<string, StreamInfo> _activeStreams = new();
        private static readonly ConcurrentDictionary<string, string> _userStreams = new();
        private static readonly ConcurrentDictionary<string, string> _streamViewers = new();
        private static readonly StreamStats _stats = new();

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"✅ Client connected: {Context.ConnectionId}");
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);

            // ارسال لیست پخش‌های فعال به کاربر جدید
            await Clients.Caller.SendAsync("StreamListUpdated", GetAvailableStreams());
            await UpdateStats();

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"❌ Client disconnected: {Context.ConnectionId}");

            // اگر کاربر یک استریمر بود
            if (_userStreams.TryGetValue(Context.ConnectionId, out var streamId))
            {
                await StopStreaming();
            }

            // اگر کاربر یک بیننده بود
            if (_streamViewers.TryRemove(Context.ConnectionId, out var viewerStreamId))
            {
                if (_activeStreams.TryGetValue(viewerStreamId, out var stream))
                {
                    stream.Viewers.RemoveAll(v => v.ConnectionId == Context.ConnectionId);
                    stream.ViewerCount = stream.Viewers.Count;
                    await Clients.Group(streamId).SendAsync("ViewerLeft", Context.ConnectionId);
                    await UpdateStreamStats(streamId);
                }
            }

            await UpdateStats();
            await base.OnDisconnectedAsync(exception);
        }

        public async Task<string> StartStreaming(string title = "پخش زنده")
        {
            var streamId = Guid.NewGuid().ToString();
            var streamInfo = new StreamInfo
            {
                StreamId = streamId,
                StreamerConnectionId = Context.ConnectionId,
                Title = title,
                StartTime = DateTime.UtcNow,
                IsLive = true
            };

            _activeStreams[streamId] = streamInfo;
            _userStreams[Context.ConnectionId] = streamId;

            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);

            Console.WriteLine($"🎬 Stream started: {streamId} by {Context.ConnectionId}");

            await Clients.Caller.SendAsync("StreamStarted", streamId);

            // ارسال به همه کاربران، حتی آن‌هایی که در گروه‌های دیگر هستند
            await SendToAllUsers("StreamListUpdated", GetAvailableStreams());
            await UpdateStats();

            return streamId;
        }

        public async Task StopStreaming()
        {
            if (_userStreams.TryRemove(Context.ConnectionId, out var streamId))
            {
                if (_activeStreams.TryRemove(streamId, out var stream))
                {
                    // به همه بینندگان اطلاع بده که استریم تمام شد
                    await Clients.Group(streamId).SendAsync("StreamEnded");

                    // بینندگان را از گروه حذف کن
                    foreach (var viewer in stream.Viewers)
                    {
                        await Groups.RemoveFromGroupAsync(viewer.ConnectionId, streamId);
                        _streamViewers.TryRemove(viewer.ConnectionId, out _);
                    }

                    Console.WriteLine($"⏹ Stream stopped: {streamId}");

                    // ارسال به همه کاربران
                    await SendToAllUsers("StreamListUpdated", GetAvailableStreams());
                    await UpdateStats();
                }
            }
        }

        public async Task JoinStream(string streamId, string viewerName = "بیننده")
        {
            if (_activeStreams.TryGetValue(streamId, out var stream))
            {
                // کاربر را به گروه استریم اضافه کن
                await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
                _streamViewers[Context.ConnectionId] = streamId;

                // بیننده جدید را اضافه کن
                var viewer = new ViewerInfo
                {
                    ConnectionId = Context.ConnectionId,
                    Name = viewerName,
                    JoinTime = DateTime.UtcNow,
                    IPAddress = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown"
                };

                stream.Viewers.Add(viewer);
                stream.ViewerCount = stream.Viewers.Count;

                await Clients.Caller.SendAsync("JoinedStream", stream);
                await Clients.OthersInGroup(streamId).SendAsync("ViewerJoined", viewer);
                await UpdateStreamStats(streamId);

                Console.WriteLine($"👤 Viewer {viewerName} joined stream: {streamId}");

                // ارسال لیست به روز شده به همه کاربران
                await SendToAllUsers("StreamListUpdated", GetAvailableStreams());
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "استریم مورد نظر پیدا نشد");
            }
        }

        public async Task LeaveStream(string streamId)
        {
            if (_streamViewers.TryRemove(Context.ConnectionId, out _))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, streamId);

                if (_activeStreams.TryGetValue(streamId, out var stream))
                {
                    stream.Viewers.RemoveAll(v => v.ConnectionId == Context.ConnectionId);
                    stream.ViewerCount = stream.Viewers.Count;

                    await Clients.Group(streamId).SendAsync("ViewerLeft", Context.ConnectionId);
                    await UpdateStreamStats(streamId);
                }

                // ارسال لیست به روز شده به همه کاربران
                await SendToAllUsers("StreamListUpdated", GetAvailableStreams());
            }
        }

        // WebRTC Signaling Methods
        public async Task SendSignal(string targetConnectionId, string type, string data)
        {
            try
            {
                Console.WriteLine($"📡 Signal sent: {type} from {Context.ConnectionId} to {targetConnectionId}");

                var signal = new WebRTCSignal
                {
                    TargetConnectionId = targetConnectionId,
                    SenderConnectionId = Context.ConnectionId,
                    Type = type,
                    Data = data
                };

                await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", signal);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending signal: {ex.Message}");
                await Clients.Caller.SendAsync("Error", "خطا در ارسال سیگنال");
            }
        }

        // متد جدید برای ارسال به همه کاربران بدون در نظر گرفتن گروه
        private async Task SendToAllUsers(string method, object data)
        {
            try
            {
                await Clients.All.SendAsync(method, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending to all users: {ex.Message}");
            }
        }

        public List<StreamInfo> GetAvailableStreams()
        {
            return _activeStreams.Values.Where(s => s.IsLive).ToList();
        }

        public StreamInfo? GetStreamInfo(string streamId)
        {
            return _activeStreams.TryGetValue(streamId, out var stream) ? stream : null;
        }

        public StreamStats GetStats()
        {
            _stats.TotalViewers = _streamViewers.Count;
            _stats.ActiveStreams = _activeStreams.Count;
            return _stats;
        }

        private async Task UpdateStats()
        {
            var stats = GetStats();
            await SendToAllUsers("StatsUpdated", stats);
        }

        private async Task UpdateStreamStats(string streamId)
        {
            if (_activeStreams.TryGetValue(streamId, out var stream))
            {
                await Clients.Group(streamId).SendAsync("StreamStatsUpdated", new
                {
                    StreamId = streamId,
                    ViewerCount = stream.ViewerCount,
                    Viewers = stream.Viewers
                });
            }
        }

        // متد جدید برای درخواست لیست پخش‌ها
        public async Task RequestStreamList()
        {
            await Clients.Caller.SendAsync("StreamListUpdated", GetAvailableStreams());
        }
    }
}
