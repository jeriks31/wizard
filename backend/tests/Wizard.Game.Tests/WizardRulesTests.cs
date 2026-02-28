using Wizard.Game;

namespace Wizard.Game.Tests;

public sealed class WizardRulesTests
{
    [Fact]
    public void CreateShuffledDeck_HasExpectedComposition()
    {
        var deck = DeckFactory.CreateShuffledDeck(new Random(42));

        Assert.Equal(60, deck.Count);
        Assert.Equal(52, deck.Count(x => x.Kind == CardKind.Standard));
        Assert.Equal(4, deck.Count(x => x.Kind == CardKind.Wizard));
        Assert.Equal(4, deck.Count(x => x.Kind == CardKind.Jester));
        foreach (var suit in Enum.GetValues<Suit>())
        {
            Assert.Single(deck.Where(x => x.Kind == CardKind.Wizard && x.Suit == suit));
            Assert.Single(deck.Where(x => x.Kind == CardKind.Jester && x.Suit == suit));
        }
    }

    [Theory]
    [InlineData(3, 20)]
    [InlineData(4, 15)]
    [InlineData(5, 12)]
    [InlineData(6, 10)]
    public void MaxRounds_UsesFloorDivision(int players, int expected)
    {
        Assert.Equal(expected, WizardRules.MaxRounds(players));
    }

    [Fact]
    public void IsBidAllowed_RejectsLastBidIfTotalWouldMatchRound()
    {
        var players = new[]
        {
            NewPlayer("p1", 0),
            NewPlayer("p2", 1),
            NewPlayer("p3", 2)
        };
        var bids = new Dictionary<string, int?>
        {
            ["p1"] = 1,
            ["p2"] = 1,
            ["p3"] = null
        };

        var allowed = WizardRules.IsBidAllowed(bids, players, 2, "p3", 0);

        Assert.False(allowed);
    }

    [Fact]
    public void IsLegalCardPlay_RequiresFollowingLeadSuit_WhenAvailable()
    {
        var hand = new List<Card>
        {
            new("S-2-10", CardKind.Standard, Suit.Hearts, 10),
            new("S-1-9", CardKind.Standard, Suit.Diamonds, 9)
        };

        var trick = new List<TrickPlay>
        {
            new() { PlayerId = "lead", SeatIndex = 0, Card = new Card("S-2-3", CardKind.Standard, Suit.Hearts, 3) }
        };

        var canPlayDiamond = WizardRules.IsLegalCardPlay(hand, hand[1], trick);

        Assert.False(canPlayDiamond);
    }

    [Fact]
    public void ResolveTrickWinner_LastWizardWins()
    {
        var trick = new List<TrickPlay>
        {
            new() { PlayerId = "p1", SeatIndex = 0, Card = new Card("W-1", CardKind.Wizard, Suit.Spades, null) },
            new() { PlayerId = "p2", SeatIndex = 1, Card = new Card("S-0-13", CardKind.Standard, Suit.Clubs, 13) },
            new() { PlayerId = "p3", SeatIndex = 2, Card = new Card("W-2", CardKind.Wizard, Suit.Diamonds, null) }
        };

        var winner = WizardRules.ResolveTrickWinner(trick, Suit.Spades);

        Assert.Equal("p3", winner);
    }

    [Fact]
    public void ResolveTrickWinner_AllJesters_LeaderWins()
    {
        var trick = new List<TrickPlay>
        {
            new() { PlayerId = "p1", SeatIndex = 0, Card = new Card("J-1", CardKind.Jester, Suit.Hearts, null) },
            new() { PlayerId = "p2", SeatIndex = 1, Card = new Card("J-2", CardKind.Jester, Suit.Diamonds, null) },
            new() { PlayerId = "p3", SeatIndex = 2, Card = new Card("J-3", CardKind.Jester, Suit.Clubs, null) }
        };

        var winner = WizardRules.ResolveTrickWinner(trick, Suit.Hearts);

        Assert.Equal("p1", winner);
    }

    [Fact]
    public void ResolveTrickWinner_TrumpBeatsLeadSuit()
    {
        var trick = new List<TrickPlay>
        {
            new() { PlayerId = "p1", SeatIndex = 0, Card = new Card("S-0-12", CardKind.Standard, Suit.Clubs, 12) },
            new() { PlayerId = "p2", SeatIndex = 1, Card = new Card("S-2-2", CardKind.Standard, Suit.Hearts, 2) },
            new() { PlayerId = "p3", SeatIndex = 2, Card = new Card("S-2-10", CardKind.Standard, Suit.Hearts, 10) }
        };

        var winner = WizardRules.ResolveTrickWinner(trick, Suit.Hearts);

        Assert.Equal("p3", winner);
    }

    [Theory]
    [InlineData(2, 2, 30)]
    [InlineData(3, 1, -20)]
    public void ScoreRound_UsesExpectedFormula(int bid, int tricksWon, int expected)
    {
        Assert.Equal(expected, WizardRules.ScoreRound(bid, tricksWon));
    }

    private static PlayerState NewPlayer(string id, int seat)
    {
        return new PlayerState
        {
            PlayerId = id,
            SeatToken = $"t-{id}",
            Name = id,
            SeatIndex = seat
        };
    }
}
