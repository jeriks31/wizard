using Wizard.Game;

namespace Wizard.Api.Contracts;

public sealed record StateUpdatedEnvelope(
    int Revision,
    string SchemaVersion,
    string Reason,
    PlayerScopedState State);

public sealed record PlayerScopedState(
    string LobbyCode,
    string Status,
    int MaxRounds,
    int? CurrentRoundNumber,
    string? CurrentTurnPlayerId,
    RoundView? Round,
    IReadOnlyList<PlayerView> Players,
    string YouPlayerId,
    bool CanStartGame,
    IReadOnlyList<int> AllowedBids,
    IReadOnlyList<string> WinnerPlayerIds,
    IReadOnlyList<RoundHistoryRowView> RoundHistory);

public sealed record RoundView(
    int RoundNumber,
    int DealerSeatIndex,
    int StartingSeatIndex,
    int CompletedTricks,
    Suit? TrumpSuit,
    CardView? UpCard,
    bool RequiresDealerTrumpSelection,
    int CurrentTrickNumber,
    string CurrentTrickLeaderPlayerId,
    string? CurrentTrickWinnerPlayerId,
    IReadOnlyList<TrickPlayView> CurrentTrickPlays);

public sealed record TrickPlayView(string PlayerId, int SeatIndex, CardView Card);
public sealed record CardView(string Id, CardKind Kind, Suit Suit, int? Value, bool IsPlayable);

public sealed record PlayerView(
    string PlayerId,
    string Name,
    int SeatIndex,
    bool IsHost,
    bool IsYou,
    bool Connected,
    int Score,
    int? Bid,
    int TricksWon,
    int HandCount,
    IReadOnlyList<CardView>? Hand);

public sealed record RoundHistoryRowView(
    int RoundNumber,
    bool IsCompleted,
    IReadOnlyList<RoundHistoryCellView> Cells);

public sealed record RoundHistoryCellView(
    string PlayerId,
    int? Bid,
    int? Score);
