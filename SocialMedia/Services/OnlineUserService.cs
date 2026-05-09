namespace SocialMedia.Services
{
    public class OnlineUserService
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary
            <string, string> _onlineUsers = new();

        public void AddUser(string userId, string connectionId)
        {
            _onlineUsers[userId] = connectionId;
        }

        public void RemoveUser(string userId)
        {
            _onlineUsers.TryRemove(userId, out _);
        }

        public bool IsOnline(string userId)
        {
            return _onlineUsers.ContainsKey(userId);
        }

        public string? GetConnectionId(string userId)
        {
            _onlineUsers.TryGetValue(userId, out var connId);
            return connId;
        }

        public List<string> GetOnlineUserIds()
        {
            return _onlineUsers.Keys.ToList();
        }
    }
}