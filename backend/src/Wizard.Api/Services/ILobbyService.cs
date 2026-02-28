using Wizard.Api.Contracts;
using Wizard.Game;

namespace Wizard.Api.Services;

public interface ILobbyService
{
    Task<JoinLobbyResponse> CreateLobbyAsync(string playerName);
    Task<JoinLobbyResponse> JoinLobbyAsync(string lobbyCode, string playerName);
    Task<LobbySnapshotResponse> GetLobbySnapshotAsync(string lobbyCode);
    Task ConnectToLobbyAsync(string lobbyCode, string playerId, string seatToken, string connectionId);
    Task StartGameAsync(string connectionId);
    Task ChooseTrumpAsync(string connectionId, Suit trumpSuit);
    Task SubmitBidAsync(string connectionId, int roundNumber, int bid);
    Task PlayCardAsync(string connectionId, int roundNumber, int trickNumber, string cardId);
    Task SendCurrentStateToCallerAsync(string connectionId);
    Task HandleDisconnectAsync(string connectionId);
}
