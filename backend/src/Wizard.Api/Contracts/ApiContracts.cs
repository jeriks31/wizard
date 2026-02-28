namespace Wizard.Api.Contracts;

public sealed record CreateLobbyRequest(string PlayerName);
public sealed record JoinLobbyRequest(string PlayerName);
public sealed record JoinLobbyResponse(string LobbyCode, string PlayerId, string SeatToken, bool IsHost);
public sealed record ErrorResponse(string Code, string Message);

public sealed record LobbySnapshotResponse(
    string LobbyCode,
    string Status,
    int Revision,
    IReadOnlyList<LobbyPlayerSummary> Players);

public sealed record LobbyPlayerSummary(
    string PlayerId,
    string Name,
    int SeatIndex,
    bool IsHost,
    bool Connected);
