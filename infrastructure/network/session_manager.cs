// infrastructure/network/session_manager.cs (итогова€ верси€)
using System.Collections.Concurrent;
using dnd_game.Application.Security;
using dnd_game.Infrastructure.Monitoring;
using Microsoft.Extensions.Logging;

namespace dnd_game.Infrastructure.Network;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly ConcurrentDictionary<Guid, Guid> _userCurrentSession = new();
    private readonly ConcurrentDictionary<Guid, List<Guid>> _connectionToUser = new(); // ConnectionId -> UserId

    private readonly PermissionChecker _permissionChecker;
    private readonly ILogger<SessionManager> _logger;
    private readonly int _maxPlayersPerSession;

    public SessionManager(
        PermissionChecker permissionChecker,
        ILogger<SessionManager> logger,
        int maxPlayersPerSession = 10)
    {
        _permissionChecker = permissionChecker;
        _logger = logger;
        _maxPlayersPerSession = maxPlayersPerSession;
    }

    public async Task<Guid> CreateSession(Guid userId, string campaignId)
    {
        var campaignGuid = Guid.Parse(campaignId);
        if (!_permissionChecker.IsGameMasterOfCampaign(campaignGuid))
            throw new UnauthorizedAccessException("Only the Game Master can create a session for this campaign.");

        var sessionId = Guid.NewGuid();
        var session = new GameSession
        {
            SessionId = sessionId,
            CampaignId = campaignGuid,
            MasterUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        session.Participants.TryAdd(userId, CampaignRole.GameMaster);
        _sessions[sessionId] = session;
        _userCurrentSession[userId] = sessionId;

        _logger.LogInformation("Session {SessionId} created for campaign {CampaignId} by master {UserId}", sessionId, campaignId, userId);
        await Task.CompletedTask;
        return sessionId;
    }

    public async Task JoinSession(Guid sessionId, Guid userId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Session not found.");
        if (!session.IsActive)
            throw new InvalidOperationException("Session is not active.");

        CampaignRole role;
        if (userId == session.MasterUserId)
            role = CampaignRole.GameMaster;
        else
        {
            if (!_permissionChecker.IsMemberOfCampaign(session.CampaignId))
                throw new UnauthorizedAccessException("You are not a member of this campaign.");
            role = CampaignRole.Player;
        }

        if (session.Participants.Count >= _maxPlayersPerSession)
            throw new InvalidOperationException("Session is full.");

        session.Participants.TryAdd(userId, role);
        _userCurrentSession[userId] = sessionId;
        _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);
        await Task.CompletedTask;
    }

    public Task LeaveSession(Guid sessionId, Guid userId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Session not found.");
        if (!session.Participants.ContainsKey(userId))
            throw new InvalidOperationException("User is not in this session.");

        session.Participants.TryRemove(userId, out _);
        if (_userCurrentSession.TryGetValue(userId, out var current) && current == sessionId)
            _userCurrentSession.TryRemove(userId, out _);

        _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);

        if (session.Participants.IsEmpty)
            session.IsActive = false;

        return Task.CompletedTask;
    }

    public Task<bool> IsUserInSession(Guid userId, Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult(session.Participants.ContainsKey(userId));
        return Task.FromResult(false);
    }

    public Task<IEnumerable<Guid>> GetSessionUsers(Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
            return Task.FromResult<IEnumerable<Guid>>(session.Participants.Keys);
        return Task.FromResult(Enumerable.Empty<Guid>());
    }

    public Task<CampaignRole?> GetUserRole(Guid userId, Guid sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session) &&
            session.Participants.TryGetValue(userId, out var role))
            return Task.FromResult<CampaignRole?>(role);
        return Task.FromResult<CampaignRole?>(null);
    }

    public async Task AssociateConnection(Guid userId, Guid sessionId, Guid connectionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            throw new InvalidOperationException("Session not found.");
        if (!session.Participants.ContainsKey(userId))
            throw new UnauthorizedAccessException("User not in session.");

        session.Connections.Add(connectionId);
        _connectionToUser.AddOrUpdate(connectionId,
            _ => new List<Guid> { userId },
            (_, list) => { list.Add(userId); return list; });

        _logger.LogDebug("Connection {ConnectionId} associated with user {UserId} in session {SessionId}", connectionId, userId, sessionId);
        await Task.CompletedTask;
    }

    public void RemoveConnection(Guid connectionId)
    {
        if (_connectionToUser.TryRemove(connectionId, out var userIds))
        {
            foreach (var userId in userIds)
            {
                if (_userCurrentSession.TryGetValue(userId, out var sessionId) &&
                    _sessions.TryGetValue(sessionId, out var session))
                {
                    // ”дал€ем соединение из сессии
                    session.Connections.TryTake(out _);
                    _logger.LogDebug("Connection {ConnectionId} removed from session {SessionId}", connectionId, sessionId);
                }
            }
        }
    }
}

// ¬спомогательный класс состо€ни€ сессии
internal class GameSession
{
    public Guid SessionId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid MasterUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public ConcurrentDictionary<Guid, CampaignRole> Participants { get; set; } = new();
    public ConcurrentBag<Guid> Connections { get; set; } = new();
}