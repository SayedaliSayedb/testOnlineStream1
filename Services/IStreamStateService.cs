using System.Collections.Concurrent;
using WebApplication1.Hubs;
using WebApplication1.Models;
using static WebApplication1.Hubs.WebRTCHub;
using ChatMessage = WebApplication1.Models.ChatMessage;
using QuizInfo = WebApplication1.Models.QuizInfo;

namespace WebApplication1.Services
{
    public interface IStreamStateService
    {
        // Stream methods
        List<StreamInfo> GetActiveStreams();
        StreamStats GetStreamStats();
        void AddStream(StreamInfo stream);
        void RemoveStream(string streamId);
        void UpdateStreamViewers(string streamId, List<ViewerInfo> viewers);
        StreamInfo GetStream(string streamId);

        // User methods - اضافه شده
        List<UserInfo> GetConnectedUsers();
        void AddUser(UserInfo user);
        void RemoveUser(string connectionId);
        UserInfo GetUser(string connectionId);
        UserInfo GetUserByNationalCode(string nationalCode);
        void UpdateUser(UserInfo user);

        // Chat methods
        void AddChatMessage(string streamId, ChatMessage message);
        List<ChatMessage> GetChatHistory(string streamId, int maxMessages = 100);
        void ClearChatHistory(string streamId);

        // Quiz methods
        void AddQuiz(string quizId, QuizInfo quiz);
        void RemoveQuiz(string quizId);
        QuizInfo GetQuiz(string quizId);
        List<QuizInfo> GetActiveQuizzes();
    }

    public class StreamStateService : IStreamStateService
    {
        private readonly ConcurrentDictionary<string, StreamInfo> _activeStreams = new();
        private readonly ConcurrentDictionary<string, UserInfo> _connectedUsers = new();
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistory = new();
        private readonly ConcurrentDictionary<string, QuizInfo> _activeQuizzes = new();
        private readonly DateTime _serverStartTime = DateTime.UtcNow;
        private readonly ILogger<StreamStateService> _logger;

        public StreamStateService(ILogger<StreamStateService> logger)
        {
            _logger = logger;
        }

        // Stream methods
        public List<StreamInfo> GetActiveStreams()
        {
            return _activeStreams.Values.Where(s => s.IsLive).ToList();
        }

        public StreamStats GetStreamStats()
        {
            var totalViewers = _activeStreams.Values.Sum(s => s.ViewerCount);

            return new StreamStats
            {
                TotalViewers = totalViewers,
                ActiveStreams = _activeStreams.Count,
                TotalUsers = _connectedUsers.Count,
                ServerStartTime = _serverStartTime,
                Uptime = DateTime.UtcNow - _serverStartTime
            };
        }

        public void AddStream(StreamInfo stream)
        {
            _activeStreams[stream.StreamId] = stream;
            _logger.LogInformation("Stream added: {StreamId}", stream.StreamId);
        }

        public void RemoveStream(string streamId)
        {
            _activeStreams.TryRemove(streamId, out _);
            _logger.LogInformation("Stream removed: {StreamId}", streamId);
        }

        public void UpdateStreamViewers(string streamId, List<ViewerInfo> viewers)
        {
            if (_activeStreams.TryGetValue(streamId, out var stream))
            {
                stream.Viewers = viewers;
                stream.ViewerCount = viewers.Count;
            }
        }

        public StreamInfo GetStream(string streamId)
        {
            _activeStreams.TryGetValue(streamId, out var stream);
            return stream;
        }

        // User methods - اضافه شده
        public List<UserInfo> GetConnectedUsers()
        {
            return _connectedUsers.Values.ToList();
        }

        public void AddUser(UserInfo user)
        {
            _connectedUsers[user.ConnectionId] = user;
            _logger.LogDebug("User added to state: {ConnectionId}", user.ConnectionId);
        }

        public void RemoveUser(string connectionId)
        {
            _connectedUsers.TryRemove(connectionId, out _);
            _logger.LogDebug("User removed from state: {ConnectionId}", connectionId);
        }

        public UserInfo GetUser(string connectionId)
        {
            _connectedUsers.TryGetValue(connectionId, out var user);
            return user;
        }

        public UserInfo GetUserByNationalCode(string nationalCode)
        {
            return _connectedUsers.Values.FirstOrDefault(u => u.ConnectionId == nationalCode);
        }

        public void UpdateUser(UserInfo user)
        {
            if (_connectedUsers.ContainsKey(user.ConnectionId))
            {
                _connectedUsers[user.ConnectionId] = user;
            }
        }

        // Chat methods - اضافه شده
        public void AddChatMessage(string streamId, ChatMessage message)
        {
            if (!_chatHistory.ContainsKey(streamId))
            {
                _chatHistory[streamId] = new List<ChatMessage>();
            }

            _chatHistory[streamId].Add(message);

            // Keep only last 100 messages per stream
            if (_chatHistory[streamId].Count > 100)
            {
                _chatHistory[streamId].RemoveAt(0);
            }
        }

        public List<ChatMessage> GetChatHistory(string streamId, int maxMessages = 100)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                return history.TakeLast(maxMessages).ToList();
            }
            return new List<ChatMessage>();
        }

        public void ClearChatHistory(string streamId)
        {
            if (_chatHistory.ContainsKey(streamId))
            {
                _chatHistory[streamId].Clear();
                _logger.LogInformation("Chat history cleared for stream: {StreamId}", streamId);
            }
        }

        // Quiz methods - اضافه شده
        public void AddQuiz(string quizId, QuizInfo quiz)
        {
            _activeQuizzes[quizId] = quiz;
            _logger.LogDebug("Quiz added: {QuizId}", quizId);
        }

        public void RemoveQuiz(string quizId)
        {
            _activeQuizzes.TryRemove(quizId, out _);
            _logger.LogDebug("Quiz removed: {QuizId}", quizId);
        }

        public QuizInfo GetQuiz(string quizId)
        {
            _activeQuizzes.TryGetValue(quizId, out var quiz);
            return quiz;
        }

        public List<QuizInfo> GetActiveQuizzes()
        {
            return _activeQuizzes.Values.Where(q => q.IsActive).ToList();
        }
    }
}
