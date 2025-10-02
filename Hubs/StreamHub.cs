using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace WebApplication1.Hubs
{
    public class StreamHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _streamers = new();
        private static int _viewerCount = 0;

        public override async Task OnConnectedAsync()
        {
            _viewerCount++;
            await Clients.All.SendAsync("ViewerCountUpdated", _viewerCount);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // اگر اتصال قطع شده مربوط به یک استریمر است، آن را حذف کن
            var streamer = _streamers.FirstOrDefault(x => x.Value == Context.ConnectionId);
            if (!string.IsNullOrEmpty(streamer.Key))
            {
                _streamers.TryRemove(streamer.Key, out _);
                await Clients.All.SendAsync("StreamEnded");
            }

            _viewerCount = Math.Max(0, _viewerCount - 1);
            await Clients.All.SendAsync("ViewerCountUpdated", _viewerCount);
            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterAsStreamer(string streamId = "default")
        {
            _streamers[streamId] = Context.ConnectionId;
            await Clients.Others.SendAsync("StreamStarted");
            Console.WriteLine($"Streamer registered: {Context.ConnectionId}");
        }

        public async Task SendStreamData(string data)
        {
            // اگر کاربر یک استریمر است، داده را به همه بینندگان بفرست
            if (_streamers.Values.Contains(Context.ConnectionId))
            {
                await Clients.Others.SendAsync("ReceiveStreamData", data);
                Console.WriteLine($"Stream data sent to viewers");
            }
        }

        public async Task<string> GetStreamStatus()
        {
            return _streamers.IsEmpty ? "offline" : "live";
        }

        public int GetViewerCount()
        {
            return _viewerCount;
        }
    }
}
