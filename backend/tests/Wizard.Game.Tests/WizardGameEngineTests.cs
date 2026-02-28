using Wizard.Game;

namespace Wizard.Game.Tests;

public sealed class WizardGameEngineTests
{
    [Fact]
    public void StartGame_InitializesRoundOne()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(1));

        engine.StartGame(state, state.Players[0].PlayerId);

        Assert.NotNull(state.Round);
        Assert.Equal(1, state.Round!.RoundNumber);
        Assert.True(state.Status is LobbyStatus.Bidding or LobbyStatus.ChoosingTrump);
        Assert.All(state.Players, player => Assert.Single(state.Round.HandsByPlayer[player.PlayerId]));
        var history = Assert.Single(state.RoundHistory);
        Assert.Equal(1, history.RoundNumber);
        Assert.False(history.IsCompleted);
        Assert.All(state.Players, player =>
        {
            Assert.Null(history.BidsByPlayer[player.PlayerId]);
            Assert.Null(history.TotalScoresByPlayer[player.PlayerId]);
        });
    }

    [Fact]
    public void SubmitBid_TransitionsToPlayingAfterAllBids()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(2));
        engine.StartGame(state, state.Players[0].PlayerId);
        EnsureBiddingState(engine, state);

        var order = state.Players.OrderBy(p => p.SeatIndex).Select(p => p.PlayerId).ToList();
        engine.SubmitBid(state, order[0], 0);
        engine.SubmitBid(state, order[1], 0);
        engine.SubmitBid(state, order[2], 0);

        Assert.Equal(LobbyStatus.Playing, state.Status);
        Assert.Equal(order[0], state.Round!.CurrentTurnPlayerId);
    }

    [Fact]
    public void SubmitBid_UpdatesCurrentRoundHistoryBid()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(2));
        engine.StartGame(state, state.Players[0].PlayerId);
        EnsureBiddingState(engine, state);

        var firstPlayerId = state.Players.OrderBy(p => p.SeatIndex).First().PlayerId;
        engine.SubmitBid(state, firstPlayerId, 0);

        var history = Assert.Single(state.RoundHistory);
        Assert.Equal(0, history.BidsByPlayer[firstPlayerId]);
        Assert.Equal(2, history.BidsByPlayer.Count(x => x.Value is null));
    }

    [Fact]
    public void PlayCard_LastCardTransitionsToResolvingTrick()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(3));
        engine.StartGame(state, state.Players[0].PlayerId);
        EnsureBiddingState(engine, state);

        SubmitZeroBids(engine, state);
        PlayCurrentTrickToResolution(engine, state);

        Assert.Equal(LobbyStatus.ResolvingTrick, state.Status);
        Assert.Equal(1, state.Round!.RoundNumber);
        Assert.Equal(1, state.Round.CompletedTricks);
        Assert.Equal(state.Players.Count, state.Round.CurrentTrick.Plays.Count);
        Assert.NotNull(state.Round.CurrentTrickWinnerPlayerId);
    }

    [Fact]
    public void AdvanceAfterTrickResolution_WhenRoundCompletes_StoresScoresAndStartsNextRound()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(3));
        engine.StartGame(state, state.Players[0].PlayerId);
        EnsureBiddingState(engine, state);

        SubmitZeroBids(engine, state);
        PlayCurrentTrickToResolution(engine, state);
        engine.AdvanceAfterTrickResolution(state);

        Assert.Equal(2, state.Round!.RoundNumber);
        Assert.True(state.Status is LobbyStatus.Bidding or LobbyStatus.ChoosingTrump);

        var roundOneHistory = state.RoundHistory.Single(x => x.RoundNumber == 1);
        Assert.True(roundOneHistory.IsCompleted);
        Assert.All(state.Players, player =>
        {
            Assert.Equal(0, roundOneHistory.BidsByPlayer[player.PlayerId]);
            Assert.Equal(player.Score, roundOneHistory.TotalScoresByPlayer[player.PlayerId]);
        });
        var roundTwoHistory = state.RoundHistory.Single(x => x.RoundNumber == 2);
        Assert.False(roundTwoHistory.IsCompleted);
        Assert.All(state.Players, player => Assert.Null(roundTwoHistory.TotalScoresByPlayer[player.PlayerId]));
    }

    [Fact]
    public void AdvanceAfterTrickResolution_WhenMoreTricksRemain_TransitionsBackToPlaying()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(5));
        engine.StartGame(state, state.Players[0].PlayerId);
        EnsureBiddingState(engine, state);

        SubmitZeroBids(engine, state);
        PlayCurrentTrickToResolution(engine, state);
        engine.AdvanceAfterTrickResolution(state);
        EnsureBiddingState(engine, state);

        SubmitZeroBids(engine, state);
        PlayCurrentTrickToResolution(engine, state);

        Assert.Equal(LobbyStatus.ResolvingTrick, state.Status);
        Assert.Equal(2, state.Round!.RoundNumber);
        Assert.Equal(1, state.Round.CompletedTricks);
        var winnerPlayerId = state.Round.CurrentTrickWinnerPlayerId;
        Assert.NotNull(winnerPlayerId);

        engine.AdvanceAfterTrickResolution(state);

        Assert.Equal(LobbyStatus.Playing, state.Status);
        Assert.Equal(2, state.Round.CurrentTrick.TrickNumber);
        Assert.Equal(1, state.Round.CompletedTricks);
        Assert.Equal(winnerPlayerId, state.Round.CurrentTrick.LeaderPlayerId);
        Assert.Equal(winnerPlayerId, state.Round.CurrentTurnPlayerId);
        Assert.Null(state.Round.CurrentTrickWinnerPlayerId);
    }

    [Fact]
    public void AdvanceAfterTrickResolution_CompletesGameWhenMaxRoundReached()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(6));
        engine.StartGame(state, state.Players[0].PlayerId);
        state.MaxRounds = 1;
        EnsureBiddingState(engine, state);

        SubmitZeroBids(engine, state);
        PlayCurrentTrickToResolution(engine, state);
        engine.AdvanceAfterTrickResolution(state);

        Assert.Equal(LobbyStatus.Completed, state.Status);
    }

    private static LobbyState NewLobbyWithPlayers(int count)
    {
        var state = new LobbyState
        {
            LobbyCode = "ABCDE1"
        };

        for (var i = 0; i < count; i++)
        {
            state.Players.Add(new PlayerState
            {
                PlayerId = $"p{i + 1}",
                SeatToken = $"token-{i + 1}",
                Name = $"Player {i + 1}",
                SeatIndex = i,
                IsHost = i == 0
            });
        }

        return state;
    }

    private static void EnsureBiddingState(WizardGameEngine engine, LobbyState state)
    {
        if (state.Status == LobbyStatus.ChoosingTrump)
        {
            var dealerPlayerId = state.Players.Single(player => player.SeatIndex == state.Round!.DealerSeatIndex).PlayerId;
            engine.ChooseTrump(state, dealerPlayerId, Suit.Spades);
        }

        Assert.Equal(LobbyStatus.Bidding, state.Status);
    }

    private static void SubmitZeroBids(WizardGameEngine engine, LobbyState state)
    {
        var playersBySeat = state.Players.OrderBy(player => player.SeatIndex).ToArray();
        var start = state.Round!.StartingSeatIndex;
        for (var i = 0; i < playersBySeat.Length; i++)
        {
            var bidder = playersBySeat[(start + i) % playersBySeat.Length];
            engine.SubmitBid(state, bidder.PlayerId, 0);
        }
    }

    private static void PlayCurrentTrickToResolution(WizardGameEngine engine, LobbyState state)
    {
        var trickNumber = state.Round!.CurrentTrick.TrickNumber;
        while (state.Status == LobbyStatus.Playing && state.Round.CurrentTrick.TrickNumber == trickNumber)
        {
            var playerId = state.Round.CurrentTurnPlayerId;
            var hand = state.Round.HandsByPlayer[playerId];
            var card = hand.First(card => WizardRules.IsLegalCardPlay(hand, card, state.Round.CurrentTrick.Plays));
            engine.PlayCard(state, playerId, card.Id);
        }
    }
}
