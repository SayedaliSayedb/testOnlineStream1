using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using WebApplication1.Controllers;
using WebApplication1.Models;

namespace WebApplication1.Hubs
{
    public class WebRTCHub : Hub
    {
        private static readonly ConcurrentDictionary<string, StreamInfo> _activeStreams = new();
        private static readonly ConcurrentDictionary<string, string> _userStreams = new();
        private static readonly ConcurrentDictionary<string, string> _streamViewers = new();
        private static readonly ConcurrentDictionary<string, UserInfo> _users = new();
        private static readonly ConcurrentDictionary<string, QuizInfo> _activeQuizzes = new();
        private static readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistory = new();
        private static readonly DateTime _serverStartTime = DateTime.UtcNow;

        // Admin controls
        private static bool _chatEnabled = true;
        private static bool _heartsEnabled = true;
        private static ContestSettings _contestSettings = new();

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"✅ Client connected: {Context.ConnectionId}");

            var userInfo = new UserInfo
            {
                ConnectionId = Context.ConnectionId,
                Name = "کاربر ناشناس",
                JoinTime = DateTime.UtcNow,
                Hearts = 3,
                Score = 0,
                IsEliminated = false,
                CanParticipate = true
            };

            _users[Context.ConnectionId] = userInfo;

            await Clients.Caller.SendAsync("Connected", Context.ConnectionId, userInfo);

            // Send current settings
            await Clients.Caller.SendAsync("ContestSettingsUpdated", _contestSettings);
            await Clients.Caller.SendAsync("ChatStatusUpdated", _chatEnabled);
            await Clients.Caller.SendAsync("HeartsStatusUpdated", _heartsEnabled);

            // Send active streams
            var streams = GetAvailableStreams();
            await Clients.Caller.SendAsync("StreamListUpdated", streams);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"❌ Client disconnected: {Context.ConnectionId}");

            // Handle streamer disconnect
            if (_userStreams.TryRemove(Context.ConnectionId, out var streamId))
            {
                if (_activeStreams.TryRemove(streamId, out var stream))
                {
                    try
                    {
                        await Clients.Group(streamId).SendAsync("StreamEnded", streamId);
                        foreach (var viewer in stream.Viewers)
                        {
                            await Groups.RemoveFromGroupAsync(viewer.ConnectionId, streamId);
                            _streamViewers.TryRemove(viewer.ConnectionId, out _);
                        }
                        await Clients.All.SendAsync("StreamListUpdated", GetAvailableStreams());
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            // Handle viewer disconnect
            if (_streamViewers.TryRemove(Context.ConnectionId, out var viewerStreamId))
            {
                if (_activeStreams.TryGetValue(viewerStreamId, out var stream))
                {
                    var viewer = stream.Viewers.FirstOrDefault(v => v.ConnectionId == Context.ConnectionId);
                    if (viewer != null)
                    {
                        stream.Viewers.Remove(viewer);
                        stream.ViewerCount = stream.Viewers.Count;
                        await UpdateStreamStats(viewerStreamId);
                    }
                }
            }

            _users.TryRemove(Context.ConnectionId, out _);
            await base.OnDisconnectedAsync(exception);
        }

        // Streaming methods
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

            // Initialize chat history for this stream
            _chatHistory[streamId] = new List<ChatMessage>();

            await Groups.AddToGroupAsync(Context.ConnectionId, streamId);
            await Clients.Caller.SendAsync("StreamStarted", streamId);
            await Clients.All.SendAsync("StreamListUpdated", GetAvailableStreams());

            return streamId;
        }

        // متد جدید برای دریافت آمار
        public AdminStats GetAdminStats()
        {
            return new AdminStats
            {
                OnlineUsers = _users.Count,
                ActiveStreams = _activeStreams.Count,
                TotalViewers = _streamViewers.Count,
                ServerUptime = DateTime.UtcNow - _serverStartTime
            };
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

                    // Remove chat history for this stream
                    _chatHistory.TryRemove(streamId, out _);

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

                // Send chat history to the new viewer
                if (_chatHistory.TryGetValue(streamId, out var history))
                {
                    await Clients.Caller.SendAsync("ChatHistory", history);
                }

                await Clients.Caller.SendAsync("JoinedStream", stream);
                await Clients.OthersInGroup(streamId).SendAsync("ViewerJoined", viewer);
                await UpdateStreamStats(streamId);
            }
            else
            {
                await Clients.Caller.SendAsync("Error", "استریم مورد نظر پیدا نشد");
            }
        }

        // Chat methods
        public async Task SendChatMessage(string message, string streamId)
        {
            if (!_chatEnabled)
            {
                await Clients.Caller.SendAsync("Error", "چت غیرفعال است");
                return;
            }

            if (_users.TryGetValue(Context.ConnectionId, out var user))
            {
                var chatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    SenderConnectionId = Context.ConnectionId,
                    SenderName = user.Name,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    StreamId = streamId
                };

                // Save to chat history
                if (_chatHistory.TryGetValue(streamId, out var history))
                {
                    history.Add(chatMessage);
                    // Keep only last 100 messages
                    if (history.Count > 100)
                    {
                        history.RemoveAt(0);
                    }
                }

                await Clients.OthersInGroup(streamId).SendAsync("ChatMessageReceived", chatMessage);
            }
        }

        // Heart methods
        public async Task SendHeart(string streamId)
        {
            if (!_heartsEnabled) return;

            await Clients.OthersInGroup(streamId).SendAsync("HeartReceived", new
            {
                SenderConnectionId = Context.ConnectionId,
                StreamId = streamId,
                Timestamp = DateTime.UtcNow
            });
        }

        // Quiz methods
        public async Task StartQuiz(QuizQuestion question, string streamId)
        {
            var quizInfo = new QuizInfo
            {
                Id = Guid.NewGuid().ToString(),
                Question = question,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddSeconds(question.TimeLimit),
                Participants = new Dictionary<string, QuizAnswer>(),
                StreamId = streamId
            };

            _activeQuizzes[quizInfo.Id] = quizInfo;

            await Clients.Group(streamId).SendAsync("QuizStarted", quizInfo);

            // Schedule end of quiz
            _ = Task.Delay(question.TimeLimit * 1000).ContinueWith(async _ =>
            {
                await EndQuiz(quizInfo.Id);
            });
        }

        public async Task SubmitQuizAnswer(string quizId, int answerIndex)
        {
            if (_activeQuizzes.TryGetValue(quizId, out var quiz) &&
                _users.TryGetValue(Context.ConnectionId, out var user))
            {
                if (quiz.Participants.ContainsKey(Context.ConnectionId) || user.IsEliminated)
                    return;

                var isCorrect = answerIndex == quiz.Question.CorrectAnswerIndex;
                var quizAnswer = new QuizAnswer
                {
                    UserConnectionId = Context.ConnectionId,
                    AnswerIndex = answerIndex,
                    IsCorrect = isCorrect,
                    AnswerTime = DateTime.UtcNow
                };

                quiz.Participants[Context.ConnectionId] = quizAnswer;

                if (isCorrect)
                {
                    user.Score += 10;
                    await Clients.Caller.SendAsync("QuizAnswerResult", true, "پاسخ صحیح! +10 امتیاز");
                }
                else
                {
                    user.Hearts = Math.Max(0, user.Hearts - 1);
                    if (user.Hearts == 0)
                    {
                        user.IsEliminated = true;
                        user.CanParticipate = false;
                        await Clients.Caller.SendAsync("Eliminated", "شما از مسابقه حذف شدید!");
                    }
                    await Clients.Caller.SendAsync("QuizAnswerResult", false, "پاسخ اشتباه! یک قلب از دست دادید");
                }

                await Clients.All.SendAsync("UserUpdated", user);
            }
        }

        private async Task EndQuiz(string quizId)
        {
            if (_activeQuizzes.TryRemove(quizId, out var quiz))
            {
                await Clients.Group(quiz.StreamId).SendAsync("QuizEnded", quiz);
            }
        }

        // Admin methods
        public async Task SetUserProfile(string name, string avatarUrl, int hearts, int score)
        {
            if (_users.TryGetValue(Context.ConnectionId, out var user))
            {
                user.Name = name;
                user.AvatarUrl = avatarUrl;
                user.Hearts = hearts;
                user.Score = score;
                user.IsEliminated = hearts == 0;

                await Clients.All.SendAsync("UserUpdated", user);
            }
        }

        public async Task KickUser(string connectionId)
        {
            if (_users.TryGetValue(connectionId, out var user))
            {
                await Clients.Client(connectionId).SendAsync("Kicked", "شما از سمت ادمین اخراج شدید");
                await Clients.Client(connectionId).SendAsync("CloseConnection");

                // Remove user from all streams
                if (_streamViewers.TryRemove(connectionId, out var streamId))
                {
                    if (_activeStreams.TryGetValue(streamId, out var stream))
                    {
                        var viewer = stream.Viewers.FirstOrDefault(v => v.ConnectionId == connectionId);
                        if (viewer != null)
                        {
                            stream.Viewers.Remove(viewer);
                            stream.ViewerCount = stream.Viewers.Count;
                            await UpdateStreamStats(streamId);
                        }
                    }
                }
            }
        }

        public async Task SetChatEnabled(bool enabled)
        {
            _chatEnabled = enabled;
            await Clients.All.SendAsync("ChatStatusUpdated", enabled);
        }

        public async Task SetHeartsEnabled(bool enabled)
        {
            _heartsEnabled = enabled;
            await Clients.All.SendAsync("HeartsStatusUpdated", enabled);
        }

        public async Task UpdateContestSettings(ContestSettings settings)
        {
            _contestSettings = settings;
            await Clients.All.SendAsync("ContestSettingsUpdated", settings);
        }

        public async Task ClearChatHistory(string streamId)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                history.Clear();
                await Clients.Group(streamId).SendAsync("ChatHistoryCleared");
            }
        }

        // WebRTC Signaling
        public async Task SendSignal(string targetConnectionId, string type, string data)
        {
            try
            {
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

        // Utility methods
        public List<StreamInfo> GetAvailableStreams()
        {
            return _activeStreams.Values.Where(s => s.IsLive).ToList();
        }

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

        public List<UserInfo> GetUsers()
        {
            return _users.Values.ToList();
        }

        public List<ChatMessage> GetChatHistory(string streamId)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                return history;
            }
            return new List<ChatMessage>();
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

    // Extended Models
    public class UserInfo
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string Name { get; set; } = "کاربر";
        public string AvatarUrl { get; set; } = string.Empty;
        public int Hearts { get; set; } = 3;
        public int Score { get; set; } = 0;
        public bool IsEliminated { get; set; } = false;
        public bool CanParticipate { get; set; } = true;
        public DateTime JoinTime { get; set; }
    }
    public class AdminStats
    {
        public int OnlineUsers { get; set; }
        public int ActiveStreams { get; set; }
        public int TotalViewers { get; set; }
        public TimeSpan ServerUptime { get; set; }
    }
    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;
        public string SenderConnectionId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string StreamId { get; set; } = string.Empty;
    }

    public class QuizQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectAnswerIndex { get; set; }
        public int TimeLimit { get; set; } = 30;
    }

    public class QuizInfo
    {
        public string Id { get; set; } = string.Empty;
        public QuizQuestion Question { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, QuizAnswer> Participants { get; set; } = new();
        public string StreamId { get; set; } = string.Empty;
    }

    public class QuizAnswer
    {
        public string UserConnectionId { get; set; } = string.Empty;
        public int AnswerIndex { get; set; }
        public bool IsCorrect { get; set; }
        public DateTime AnswerTime { get; set; }
    }

    public class ContestSettings
    {
        public DateTime ContestStartTime { get; set; } = DateTime.UtcNow.AddHours(1);
        public int InitialHearts { get; set; } = 3;
        public bool RegistrationOpen { get; set; } = true;
        public string ContestTitle { get; set; } = "مسابقه پخش زنده";
    }
}
