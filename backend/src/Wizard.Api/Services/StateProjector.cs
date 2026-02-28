using Wizard.Api.Contracts;
using Wizard.Game;

namespace Wizard.Api.Services;

internal static class StateProjector
{
    public static StateUpdatedEnvelope BuildEnvelope(LobbyState state, string recipientPlayerId, string reason)
    {
        var playerViews = state.Players
            .OrderBy(p => p.SeatIndex)
            .Select(player =>
            {
                var hand = state.Round?.HandsByPlayer.GetValueOrDefault(player.PlayerId);
                var isYou = player.PlayerId == recipientPlayerId;
                var canAct =
                    state.Status == LobbyStatus.Playing &&
                    state.Round is not null &&
                    state.Round.CurrentTurnPlayerId == recipientPlayerId;
                return new PlayerView(
                    player.PlayerId,
                    player.Name,
                    player.SeatIndex,
                    player.IsHost,
                    isYou,
                    player.Connected,
                    player.Score,
                    player.CurrentBid,
                    player.TricksWonThisRound,
                    hand?.Count ?? 0,
                    isYou && hand is not null
                        ? hand
                            .Select(card =>
                                ToCardView(
                                    card,
                                    canAct && state.Round is not null && WizardRules.IsLegalCardPlay(hand, card, state.Round.CurrentTrick.Plays)))
                            .ToArray()
                        : null);
            })
            .ToArray();

        var winners = state.Status == LobbyStatus.Completed
            ? ResolveWinners(state.Players)
            : [];

        var round = state.Round is null
            ? null
            : new RoundView(
                state.Round.RoundNumber,
                state.Round.DealerSeatIndex,
                state.Round.StartingSeatIndex,
                state.Round.CompletedTricks,
                state.Round.TrumpSuit,
                state.Round.UpCard is null ? null : ToCardView(state.Round.UpCard),
                state.Round.RequiresDealerTrumpSelection,
                state.Round.CurrentTrick.TrickNumber,
                state.Round.CurrentTrick.LeaderPlayerId,
                state.Round.CurrentTrick.Plays
                    .Select(play => new TrickPlayView(play.PlayerId, play.SeatIndex, ToCardView(play.Card)))
                    .ToArray());

        var stateView = new PlayerScopedState(
            state.LobbyCode,
            state.Status.ToString(),
            state.MaxRounds,
            state.Round?.RoundNumber,
            state.Round?.CurrentTurnPlayerId,
            round,
            playerViews,
            recipientPlayerId,
            state.Status == LobbyStatus.Lobby && state.Players.Any(x => x.PlayerId == recipientPlayerId && x.IsHost) && state.Players.Count is >= 3 and <= 6,
            GetAllowedBids(state, recipientPlayerId),
            winners);

        return new StateUpdatedEnvelope(
            state.Revision,
            "v1",
            reason,
            stateView);
    }

    private static CardView ToCardView(Card card, bool isPlayable = false)
    {
        return new CardView(card.Id, card.Kind, card.Suit, card.Value, isPlayable);
    }

    private static IReadOnlyList<string> ResolveWinners(IReadOnlyList<PlayerState> players)
    {
        if (players.Count == 0)
        {
            return [];
        }

        var bestScore = players.Max(x => x.Score);
        return players.Where(x => x.Score == bestScore).Select(x => x.PlayerId).ToArray();
    }

    private static IReadOnlyList<int> GetAllowedBids(LobbyState state, string recipientPlayerId)
    {
        if (state.Status != LobbyStatus.Bidding || state.Round is null)
        {
            return [];
        }

        if (state.Round.CurrentTurnPlayerId != recipientPlayerId)
        {
            return [];
        }

        var playersBySeat = state.Players
            .OrderBy(p => p.SeatIndex)
            .ToList();

        return Enumerable
            .Range(0, state.Round.RoundNumber + 1)
            .Where(bid => WizardRules.IsBidAllowed(
                state.Round.BidsByPlayer,
                playersBySeat,
                state.Round.RoundNumber,
                recipientPlayerId,
                bid))
            .ToArray();
    }
}
