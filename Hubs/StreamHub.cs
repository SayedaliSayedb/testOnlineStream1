using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace WebApplication1.Hubs
{
    public class StreamHub : Hub
    {
        private static readonly ConcurrentDictionary<string, StreamerInfo> _streamers = new();
        private static readonly ConcurrentDictionary<string, string> _viewers = new();

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // اگر استریمر قطع شد
            if (_streamers.ContainsKey(Context.ConnectionId))
            {
                _streamers.TryRemove(Context.ConnectionId, out _);
                await Clients.All.SendAsync("StreamerDisconnected", Context.ConnectionId);
            }

            // اگر بیننده قطع شد
            if (_viewers.ContainsKey(Context.ConnectionId))
            {
                _viewers.TryRemove(Context.ConnectionId, out _);
            }

            await UpdateViewerCount();
            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterAsStreamer()
        {
            _streamers[Context.ConnectionId] = new StreamerInfo
            {
                ConnectionId = Context.ConnectionId,
                StartTime = DateTime.UtcNow
            };

            await Clients.Caller.SendAsync("StreamerRegistered");
            await Clients.Others.SendAsync("NewStreamerAvailable", Context.ConnectionId);
        }

        public async Task RegisterAsViewer(string streamerId)
        {
            _viewers[Context.ConnectionId] = streamerId;
            await UpdateViewerCount();
        }

        // WebRTC Signaling - برای مذاکره ارتباط مستقیم بین کلاینت‌ها
        public async Task SendOffer(string targetConnectionId, string offer)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveOffer", Context.ConnectionId, offer);
        }

        public async Task SendAnswer(string targetConnectionId, string answer)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveAnswer", Context.ConnectionId, answer);
        }

        public async Task SendIceCandidate(string targetConnectionId, string candidate)
        {
            await Clients.Client(targetConnectionId).SendAsync("ReceiveIceCandidate", Context.ConnectionId, candidate);
        }

        public async Task<List<string>> GetAvailableStreamers()
        {
            return _streamers.Keys.ToList();
        }

        private async Task UpdateViewerCount()
        {
            var viewerCount = _viewers.Count;
            await Clients.All.SendAsync("ViewerCountUpdated", viewerCount);
        }
    }

    public class StreamerInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public int ViewerCount { get; set; }
    }
}
