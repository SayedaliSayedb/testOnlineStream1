using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WebApplication1.Models;
using WebApplication1.Race;
using WebApplication1.Race.Services;

namespace WebApplication1.Hubs
{
    public class WebRTCHub : Hub
    {
        private static readonly ConcurrentDictionary<string, StreamInfo> _activeStreams = new();
        private static readonly ConcurrentDictionary<string, string> _userStreams = new();
        private static readonly ConcurrentDictionary<string, string> _streamViewers = new();
        private static readonly DateTime _serverStartTime = DateTime.UtcNow;
        private readonly IQuizDataService _dataService;
        private static readonly Dictionary<string, Participant> _participants = new();
        private static QuizState _currentState = new();
        private static bool _isInitialized = false;
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"✅ Client connected: {Context.ConnectionId}");
            await Clients.Caller.SendAsync("Connected", Context.ConnectionId);

            // ارسال لیست پخش‌های فعال به کاربر جدید
            var streams = GetAvailableStreams();
            await Clients.Caller.SendAsync("StreamListUpdated", streams);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"❌ Client disconnected: {Context.ConnectionId}");

            // اگر کاربر یک استریمر بود
            if (_userStreams.TryRemove(Context.ConnectionId, out var streamId))
            {
                if (_activeStreams.TryRemove(streamId, out var stream))
                {
                    // به همه بینندگان اطلاع بده که استریم تمام شد
                    try
                    {
                        await Clients.Group(streamId).SendAsync("StreamEnded", streamId);

                        // بینندگان را از گروه حذف کن
                        foreach (var viewer in stream.Viewers)
                        {
                            await Groups.RemoveFromGroupAsync(viewer.ConnectionId, streamId);
                            _streamViewers.TryRemove(viewer.ConnectionId, out _);
                        }

                        Console.WriteLine($"⏹ Stream stopped: {streamId}");

                        // به همه کاربران اطلاع بده
                        await Clients.All.SendAsync("StreamListUpdated", GetAvailableStreams());
                    }
                    catch (Exception)
                    {

                    }
                }
            }


            // اگر کاربر یک بیننده بود
            if (_streamViewers.TryRemove(Context.ConnectionId, out var viewerStreamId))
            {
                if (_activeStreams.TryGetValue(viewerStreamId, out var stream))
                {
                    var viewer = stream.Viewers.FirstOrDefault(v => v.ConnectionId == Context.ConnectionId);
                    if (viewer != null)
                    {
                        stream.Viewers.Remove(viewer);
                        stream.ViewerCount = stream.Viewers.Count;

                        await Clients.Group(viewerStreamId).SendAsync("ViewerLeft", Context.ConnectionId);
                        await UpdateStreamStats(viewerStreamId);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }
        private async Task InitializeFromStorage()
        {
            var savedState = await _dataService.GetQuizStateAsync();
            if (savedState != null)
            {
                _currentState = savedState;
            }
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
                IsLive = true,
                ViewerCount = 0,
                Viewers = new List<ViewerInfo>()
            };

            _activeStreams[streamId] = streamInfo;
            _userStreams[Context.ConnectionId] = streamId;

            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);

            Console.WriteLine($"🎬 Stream started: {streamId} by {Context.ConnectionId}");

            await Clients.Caller.SendAsync("StreamStarted", streamId);

            // اطلاع به همه کاربران
            await Clients.All.SendAsync("StreamListUpdated", GetAvailableStreams());

            return streamId;
        }

        public async Task StopStreaming()
        {
            if (_userStreams.TryRemove(Context.ConnectionId, out var streamId))
            {
                if (_activeStreams.TryRemove(streamId, out var stream))
                {
                    await Clients.Group(streamId).SendAsync("StreamEnded", streamId);

                    foreach (var viewer in stream.Viewers)
                    {
                        await Groups.RemoveFromGroupAsync(viewer.ConnectionId, streamId);
                        _streamViewers.TryRemove(viewer.ConnectionId, out _);
                    }

                    Console.WriteLine($"⏹ Stream stopped: {streamId}");
                    await Clients.All.SendAsync("StreamListUpdated", GetAvailableStreams());
                }
            }
        }

        public async Task JoinStream(string streamId, string viewerName = "بیننده")
        {
            if (_activeStreams.TryGetValue(streamId, out var stream))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
                _streamViewers[Context.ConnectionId] = streamId;

                var viewer = new ViewerInfo
                {
                    ConnectionId = Context.ConnectionId,
                    Name = viewerName,
                    JoinTime = DateTime.UtcNow,
                    IPAddress = Context.GetHttpContext()?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown"
                };

                stream.Viewers.Add(viewer);
                stream.ViewerCount = stream.Viewers.Count;

                // ارسال اطلاعات استریم به بیننده
                await Clients.Caller.SendAsync("JoinedStream", stream);

                await Clients.OthersInGroup(streamId).SendAsync("ViewerJoined", viewer);
                await UpdateStreamStats(streamId);

                Console.WriteLine($"👤 Viewer joined: {viewerName} to {streamId}");
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
                    var viewer = stream.Viewers.FirstOrDefault(v => v.ConnectionId == Context.ConnectionId);
                    if (viewer != null)
                    {
                        stream.Viewers.Remove(viewer);
                        stream.ViewerCount = stream.Viewers.Count;
                        await UpdateStreamStats(streamId);
                    }
                }
            }
        }

        // WebRTC Signaling
        public async Task SendSignal(string targetConnectionId, string type, string data)
        {
            try
            {
                Console.WriteLine($"📡 Signal: {type} from {Context.ConnectionId} to {targetConnectionId}");

                await Clients.Client(targetConnectionId).SendAsync("ReceiveSignal", new
                {
                    SenderConnectionId = Context.ConnectionId,
                    Type = type,
                    Data = data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending signal: {ex.Message}");
            }
        }

        public List<StreamInfo> GetAvailableStreams()
        {
            return _activeStreams.Values.Where(s => s.IsLive).ToList();
        }

        // اضافه کردن متد GetStats
        public StreamStats GetStats()
        {
            return new StreamStats
            {
                TotalViewers = _streamViewers.Count,
                ActiveStreams = _activeStreams.Count,
                ServerStartTime = _serverStartTime,
                Uptime = DateTime.UtcNow - _serverStartTime
            };
        }

        public async Task RequestStreamList()
        {
            var streams = GetAvailableStreams();
            await Clients.Caller.SendAsync("StreamListUpdated", streams);
        }

        private async Task UpdateStreamStats(string streamId)
        {
            if (_activeStreams.TryGetValue(streamId, out var stream))
            {
                await Clients.Group(streamId).SendAsync("StreamStatsUpdated", new
                {
                    StreamId = streamId,
                    ViewerCount = stream.ViewerCount
                });
            }
        }
    }
}
