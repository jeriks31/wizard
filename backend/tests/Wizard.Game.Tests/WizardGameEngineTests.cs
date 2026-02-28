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
        Assert.Equal(LobbyStatus.Bidding, state.Status);
        Assert.All(state.Players, player => Assert.Single(state.Round.HandsByPlayer[player.PlayerId]));
    }

    [Fact]
    public void SubmitBid_TransitionsToPlayingAfterAllBids()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(2));
        engine.StartGame(state, state.Players[0].PlayerId);

        var order = state.Players.OrderBy(p => p.SeatIndex).Select(p => p.PlayerId).ToList();
        engine.SubmitBid(state, order[0], 0);
        engine.SubmitBid(state, order[1], 0);
        engine.SubmitBid(state, order[2], 0);

        Assert.Equal(LobbyStatus.Playing, state.Status);
        Assert.Equal(order[0], state.Round!.CurrentTurnPlayerId);
    }

    [Fact]
    public void PlayCard_CompletesTrickAndStartsNextRound()
    {
        var state = NewLobbyWithPlayers(3);
        var engine = new WizardGameEngine(new Random(3));
        engine.StartGame(state, state.Players[0].PlayerId);

        var turnOrder = state.Players.OrderBy(p => p.SeatIndex).Select(p => p.PlayerId).ToArray();
        engine.SubmitBid(state, turnOrder[0], 0);
        engine.SubmitBid(state, turnOrder[1], 0);
        engine.SubmitBid(state, turnOrder[2], 0);

        var first = state.Round!.HandsByPlayer[turnOrder[0]][0].Id;
        var second = state.Round.HandsByPlayer[turnOrder[1]][0].Id;
        var third = state.Round.HandsByPlayer[turnOrder[2]][0].Id;

        engine.PlayCard(state, turnOrder[0], first);
        engine.PlayCard(state, turnOrder[1], second);
        engine.PlayCard(state, turnOrder[2], third);

        Assert.Equal(2, state.Round.RoundNumber);
        Assert.True(state.Players.Any(p => p.Score != 0));
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
}
