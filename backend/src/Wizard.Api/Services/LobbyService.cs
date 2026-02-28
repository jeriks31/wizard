using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Wizard.Api.Contracts;
using Wizard.Api.Hubs;
using Wizard.Game;

namespace Wizard.Api.Services;

public sealed class LobbyService : ILobbyService
{
    private readonly ConcurrentDictionary<string, LobbyActor> _lobbies = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ConnectionSession> _sessionsByConnection = new();
    private readonly IHubContext<GameHub> _hubContext;

    public LobbyService(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task<JoinLobbyResponse> CreateLobbyAsync(string playerName)
    {
        ValidatePlayerName(playerName);

        while (true) // Loop in case of lobbyCode collision
        {
            var lobbyCode = GenerateLobbyCode();
            var host = new PlayerState
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                SeatToken = Guid.NewGuid().ToString("N"),
                Name = playerName.Trim(),
                SeatIndex = 0,
                IsHost = true
            };

            var state = new LobbyState
            {
                LobbyCode = lobbyCode
            };
            state.Players.Add(host);
            var actor = new LobbyActor(state);

            if (_lobbies.TryAdd(lobbyCode, actor))
            {
                return await Task.FromResult(new JoinLobbyResponse(lobbyCode, host.PlayerId, host.SeatToken, true));
            }
        }
    }

    public async Task<JoinLobbyResponse> JoinLobbyAsync(string lobbyCode, string playerName)
    {
        ValidatePlayerName(playerName);
        var actor = GetLobbyOrThrow(lobbyCode);

        var response = await actor.RunAsync(state =>
        {
            if (state.Status != LobbyStatus.Lobby)
            {
                throw new InvalidOperationException("Game already started.");
            }

            if (state.Players.Count >= 6)
            {
                throw new InvalidOperationException("Lobby is full.");
            }

            var player = new PlayerState
            {
                PlayerId = Guid.NewGuid().ToString("N"),
                SeatToken = Guid.NewGuid().ToString("N"),
                Name = playerName.Trim(),
                SeatIndex = state.Players.Count,
                IsHost = false
            };
            state.Players.Add(player);
            state.Revision++;
            return new JoinLobbyResponse(state.LobbyCode, player.PlayerId, player.SeatToken, false);
        });

        await BroadcastStateAsync(actor, "PlayerJoined");
        return response;
    }

    public async Task<LobbySnapshotResponse> GetLobbySnapshotAsync(string lobbyCode)
    {
        var actor = GetLobbyOrThrow(lobbyCode);
        return await actor.RunAsync(state =>
        {
            var players = state.Players
                .OrderBy(p => p.SeatIndex)
                .Select(p => new LobbyPlayerSummary(p.PlayerId, p.Name, p.SeatIndex, p.IsHost, p.Connected))
                .ToArray();
            return new LobbySnapshotResponse(state.LobbyCode, state.Status.ToString(), state.Revision, players);
        });
    }

    public async Task ConnectToLobbyAsync(string lobbyCode, string playerId, string seatToken, string connectionId)
    {
        var actor = GetLobbyOrThrow(lobbyCode);

        await actor.RunAsync(state =>
        {
            var player = state.Players.SingleOrDefault(p => p.PlayerId == playerId)
                ?? throw new InvalidOperationException("Player does not belong to lobby.");

            if (!string.Equals(player.SeatToken, seatToken, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Invalid reconnect token.");
            }

            player.Connected = true;
            player.ConnectionId = connectionId;
            state.Revision++;
            return true;
        });

        _sessionsByConnection[connectionId] = new ConnectionSession(lobbyCode.ToUpperInvariant(), playerId);
        await BroadcastStateAsync(actor, "Connected");
    }

    public async Task StartGameAsync(string connectionId)
    {
        var session = GetSession(connectionId);
        var actor = GetLobbyOrThrow(session.LobbyCode);
        await actor.RunAsync(state =>
        {
            EnsureActiveConnection(state, session.PlayerId, connectionId);
            var engine = new WizardGameEngine(Random.Shared);
            engine.StartGame(state, session.PlayerId);
            state.Revision++;
            return true;
        });

        await BroadcastStateAsync(actor, "GameStarted");
    }

    public async Task ChooseTrumpAsync(string connectionId, Suit trumpSuit)
    {
        var session = GetSession(connectionId);
        var actor = GetLobbyOrThrow(session.LobbyCode);
        await actor.RunAsync(state =>
        {
            EnsureActiveConnection(state, session.PlayerId, connectionId);
            var engine = new WizardGameEngine(Random.Shared);
            engine.ChooseTrump(state, session.PlayerId, trumpSuit);
            state.Revision++;
            return true;
        });
        await BroadcastStateAsync(actor, "RoundAdvanced");
    }

    public async Task SubmitBidAsync(string connectionId, int roundNumber, int bid)
    {
        var session = GetSession(connectionId);
        var actor = GetLobbyOrThrow(session.LobbyCode);
        await actor.RunAsync(state =>
        {
            EnsureActiveConnection(state, session.PlayerId, connectionId);
            if (state.Round?.RoundNumber != roundNumber)
            {
                throw new InvalidOperationException("Round number mismatch.");
            }

            var engine = new WizardGameEngine(Random.Shared);
            engine.SubmitBid(state, session.PlayerId, bid);
            state.Revision++;
            return true;
        });
        await BroadcastStateAsync(actor, "BidSubmitted");
    }

    public async Task PlayCardAsync(string connectionId, int roundNumber, int trickNumber, string cardId)
    {
        var session = GetSession(connectionId);
        var actor = GetLobbyOrThrow(session.LobbyCode);
        string reason = "CardPlayed";

        await actor.RunAsync(state =>
        {
            EnsureActiveConnection(state, session.PlayerId, connectionId);
            if (state.Round?.RoundNumber != roundNumber)
            {
                throw new InvalidOperationException("Round number mismatch.");
            }

            if (state.Round.CurrentTrick.TrickNumber != trickNumber)
            {
                throw new InvalidOperationException("Trick number mismatch.");
            }

            var oldRound = state.Round.RoundNumber;
            var oldCompleted = state.Round.CompletedTricks;

            var engine = new WizardGameEngine(Random.Shared);
            engine.PlayCard(state, session.PlayerId, cardId);
            state.Revision++;

            if (state.Status == LobbyStatus.Completed)
            {
                reason = "GameEnded";
            }
            else if (state.Round?.RoundNumber != oldRound || state.Round?.CompletedTricks != oldCompleted)
            {
                reason = "RoundAdvanced";
            }

            return true;
        });

        await BroadcastStateAsync(actor, reason);
    }

    public async Task SendCurrentStateToCallerAsync(string connectionId)
    {
        var session = GetSession(connectionId);
        var actor = GetLobbyOrThrow(session.LobbyCode);
        StateUpdatedEnvelope envelope = await actor.RunAsync(state =>
        {
            EnsureActiveConnection(state, session.PlayerId, connectionId);
            return StateProjector.BuildEnvelope(state, session.PlayerId, "Resync");
        });

        await _hubContext.Clients.Client(connectionId).SendAsync("StateUpdated", envelope);
    }

    public async Task HandleDisconnectAsync(string connectionId)
    {
        if (!_sessionsByConnection.TryRemove(connectionId, out var session))
        {
            return;
        }

        if (!_lobbies.TryGetValue(session.LobbyCode, out var actor))
        {
            return;
        }

        var didMutate = await actor.RunAsync(state =>
        {
            var player = state.Players.SingleOrDefault(x => x.PlayerId == session.PlayerId);
            if (player is null || player.ConnectionId != connectionId)
            {
                return false;
            }

            player.ConnectionId = null;
            player.Connected = false;
            state.Revision++;
            return true;
        });

        if (didMutate)
        {
            await BroadcastStateAsync(actor, "Disconnected");
        }
    }

    private async Task BroadcastStateAsync(LobbyActor actor, string reason)
    {
        var payloads = await actor.RunAsync(state =>
        {
            return state.Players
                .Where(player => player.Connected && !string.IsNullOrWhiteSpace(player.ConnectionId))
                .Select(player => new ConnectionPayload(
                    player.ConnectionId!,
                    StateProjector.BuildEnvelope(state, player.PlayerId, reason)))
                .ToArray();
        });

        foreach (var payload in payloads)
        {
            await _hubContext.Clients.Client(payload.ConnectionId).SendAsync("StateUpdated", payload.Envelope);
        }
    }

    private static void EnsureActiveConnection(LobbyState state, string playerId, string connectionId)
    {
        var player = state.Players.SingleOrDefault(p => p.PlayerId == playerId)
            ?? throw new InvalidOperationException("Player not found.");

        if (!player.Connected || player.ConnectionId != connectionId)
        {
            throw new InvalidOperationException("Connection is no longer active for this player.");
        }
    }

    private LobbyActor GetLobbyOrThrow(string lobbyCode)
    {
        if (_lobbies.TryGetValue(lobbyCode.ToUpperInvariant(), out var actor))
        {
            return actor;
        }

        throw new KeyNotFoundException("Lobby not found.");
    }

    private static void ValidatePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            throw new InvalidOperationException("Player name is required.");
        }

        if (playerName.Trim().Length > 32)
        {
            throw new InvalidOperationException("Player name must be 32 characters or fewer.");
        }
    }

    private static string GenerateLobbyCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Non-similar looking chars
        return string.Create(6, chars, static (span, chars) =>
        {
            var random = Random.Shared;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = chars[random.Next(chars.Length)];
            }
        });
    }

    private ConnectionSession GetSession(string connectionId)
    {
        if (_sessionsByConnection.TryGetValue(connectionId, out var session))
        {
            return session;
        }

        throw new InvalidOperationException("Connection has not joined a lobby.");
    }

    private sealed record ConnectionSession(string LobbyCode, string PlayerId);
    private sealed record ConnectionPayload(string ConnectionId, StateUpdatedEnvelope Envelope);
}
