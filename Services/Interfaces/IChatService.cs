using System.Collections.Concurrent;
using WebApplication1.Models;

namespace WebApplication1.Services.Interfaces
{
    public interface IChatService
    {
        Task<ChatMessage> SaveMessageAsync(ChatMessage message);
        Task<List<ChatMessage>> GetChatHistoryAsync(string streamId, int maxMessages = 100);
        Task<bool> ClearChatHistoryAsync(string streamId);
        Task<int> GetMessageCountAsync(string streamId);
        Task<List<ChatMessage>> GetUserMessagesAsync(string nationalCode, string streamId);
    }

    public class ChatService : IChatService
    {
        private readonly ConcurrentDictionary<string, List<ChatMessage>> _chatHistory = new();
        private readonly ILogger<ChatService> _logger;

        public ChatService(ILogger<ChatService> logger)
        {
            _logger = logger;
        }

        public Task<ChatMessage> SaveMessageAsync(ChatMessage message)
        {
            if (!_chatHistory.ContainsKey(message.StreamId))
            {
                _chatHistory[message.StreamId] = new List<ChatMessage>();
            }

            _chatHistory[message.StreamId].Add(message);

            // Keep only last 100 messages per stream
            if (_chatHistory[message.StreamId].Count > 100)
            {
                _chatHistory[message.StreamId].RemoveAt(0);
            }

            _logger.LogDebug("Chat message saved for stream: {StreamId}, Sender: {Sender}",
                message.StreamId, message.SenderName);

            return Task.FromResult(message);
        }

        public Task<List<ChatMessage>> GetChatHistoryAsync(string streamId, int maxMessages = 100)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                return Task.FromResult(history.TakeLast(maxMessages).ToList());
            }

            return Task.FromResult(new List<ChatMessage>());
        }

        public Task<bool> ClearChatHistoryAsync(string streamId)
        {
            if (_chatHistory.ContainsKey(streamId))
            {
                _chatHistory[streamId].Clear();
                _logger.LogInformation("Chat history cleared for stream: {StreamId}", streamId);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<int> GetMessageCountAsync(string streamId)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                return Task.FromResult(history.Count);
            }

            return Task.FromResult(0);
        }

        public Task<List<ChatMessage>> GetUserMessagesAsync(string nationalCode, string streamId)
        {
            if (_chatHistory.TryGetValue(streamId, out var history))
            {
                var userMessages = history
                    .Where(m => m.SenderNationalCode == nationalCode)
                    .ToList();

                return Task.FromResult(userMessages);
            }

            return Task.FromResult(new List<ChatMessage>());
        }
    }
}
