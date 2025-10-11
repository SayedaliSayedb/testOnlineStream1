using System.Collections.Concurrent;

namespace SiteBarnaQ.Services
{
    public interface IConnectionManager
    {
        void AddConnection(string connectionId);
        void RemoveConnection(string connectionId);
        bool IsConnected(string connectionId);
        int TotalConnections { get; }
        IReadOnlyList<string> ActiveConnections { get; }
    }

    public class ConnectionManager : IConnectionManager
    {
        private readonly ConcurrentDictionary<string, DateTime> _connections = new();
        private readonly ILogger<ConnectionManager> _logger;

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
        }

        public void AddConnection(string connectionId)
        {
            _connections[connectionId] = DateTime.UtcNow;
            _logger.LogDebug("Connection added: {ConnectionId}. Total: {Total}",
                connectionId, _connections.Count);
        }

        public void RemoveConnection(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
            _logger.LogDebug("Connection removed: {ConnectionId}. Total: {Total}",
                connectionId, _connections.Count);
        }

        public bool IsConnected(string connectionId)
        {
            return _connections.ContainsKey(connectionId);
        }

        public int TotalConnections => _connections.Count;

        public IReadOnlyList<string> ActiveConnections => _connections.Keys.ToList();
    }
}
