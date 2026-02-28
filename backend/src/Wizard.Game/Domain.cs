using System.Collections.ObjectModel;

namespace Wizard.Game;

public enum LobbyStatus
{
    Lobby = 0,
    ChoosingTrump = 1,
    Bidding = 2,
    Playing = 3,
    Completed = 4
}

public enum CardKind
{
    Standard = 0,
    Wizard = 1,
    Jester = 2
}

public enum Suit
{
    Clubs = 0,
    Diamonds = 1,
    Hearts = 2,
    Spades = 3
}

public sealed record Card(string Id, CardKind Kind, Suit Suit, int? Value);

public sealed class PlayerState
{
    public required string PlayerId { get; init; }
    public required string SeatToken { get; init; }
    public required string Name { get; init; }
    public required int SeatIndex { get; init; }
    public bool IsHost { get; set; }
    public bool Connected { get; set; } = true;
    public string? ConnectionId { get; set; }
    public int Score { get; set; }
    public int TricksWonThisRound { get; set; }
    public int? CurrentBid { get; set; }
}

public sealed class TrickPlay
{
    public required string PlayerId { get; init; }
    public required int SeatIndex { get; init; }
    public required Card Card { get; init; }
}

public sealed class TrickState
{
    public required int TrickNumber { get; init; }
    public required string LeaderPlayerId { get; init; }
    public List<TrickPlay> Plays { get; } = [];
}

public sealed class RoundState
{
    public required int RoundNumber { get; init; }
    public required int DealerSeatIndex { get; init; }
    public required int StartingSeatIndex { get; init; }
    public Suit? TrumpSuit { get; set; }
    public Card? UpCard { get; set; }
    public bool RequiresDealerTrumpSelection { get; set; }
    public required string CurrentTurnPlayerId { get; set; }
    public required TrickState CurrentTrick { get; set; }
    public int CompletedTricks { get; set; }
    public Dictionary<string, List<Card>> HandsByPlayer { get; } = [];
    public Dictionary<string, int?> BidsByPlayer { get; } = [];
    public Dictionary<string, int> TricksWonByPlayer { get; } = [];
}

public sealed class RoundHistoryEntry
{
    public required int RoundNumber { get; init; }
    public Dictionary<string, int?> BidsByPlayer { get; } = [];
    public Dictionary<string, int?> TotalScoresByPlayer { get; } = [];
    public bool IsCompleted { get; set; }
}

public sealed class LobbyState
{
    public required string LobbyCode { get; init; }
    public LobbyStatus Status { get; set; } = LobbyStatus.Lobby;
    public int Revision { get; set; }
    public int MaxRounds { get; set; }
    public List<PlayerState> Players { get; } = [];
    public List<RoundHistoryEntry> RoundHistory { get; } = [];
    public RoundState? Round { get; set; }
}

public static class DeckFactory
{
    public static List<Card> CreateShuffledDeck(Random random)
    {
        var cards = new List<Card>(60);

        foreach (var suit in Enum.GetValues<Suit>())
        {
            for (var value = 1; value <= 13; value++)
            {
                cards.Add(new Card($"S-{(int)suit}-{value}", CardKind.Standard, suit, value));
            }
        }

        for (var i = 0; i < 4; i++)
        {
            var suit = (Suit)i;
            cards.Add(new Card($"W-{i}", CardKind.Wizard, suit, null));
            cards.Add(new Card($"J-{i}", CardKind.Jester, suit, null));
        }

        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }
}

public static class WizardRules
{
    public static int MaxRounds(int playerCount)
    {
        if (playerCount < 3 || playerCount > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(playerCount), "Wizard supports 3-6 players.");
        }

        return 60 / playerCount;
    }

    public static bool IsBidAllowed(
        IReadOnlyDictionary<string, int?> currentBids,
        IReadOnlyList<PlayerState> playersBySeat,
        int roundNumber,
        string biddingPlayerId,
        int bid)
    {
        if (bid < 0 || bid > roundNumber)
        {
            return false;
        }

        var missingBids = playersBySeat.Count(p => !currentBids.TryGetValue(p.PlayerId, out var maybeBid) || maybeBid is null);
        if (missingBids != 1)
        {
            return true;
        }

        var expectedLastBidder = playersBySeat.FirstOrDefault(
            p => !currentBids.TryGetValue(p.PlayerId, out var maybeBid) || maybeBid is null);

        if (expectedLastBidder is null || expectedLastBidder.PlayerId != biddingPlayerId)
        {
            return true;
        }

        var runningTotal = currentBids.Values.Where(v => v.HasValue).Sum(v => v!.Value);
        return runningTotal + bid != roundNumber;
    }

    public static bool IsLegalCardPlay(
        IReadOnlyList<Card> hand,
        Card selectedCard,
        IReadOnlyList<TrickPlay> currentTrickPlays)
    {
        if (!hand.Any(c => c.Id == selectedCard.Id))
        {
            return false;
        }

        if (selectedCard.Kind is CardKind.Wizard or CardKind.Jester)
        {
            return true;
        }

        var leadSuit = LeadSuit(currentTrickPlays);
        if (leadSuit is null)
        {
            return true;
        }

        if (selectedCard.Suit == leadSuit)
        {
            return true;
        }

        var handHasLeadSuit = hand.Any(c => c.Kind == CardKind.Standard && c.Suit == leadSuit);
        return !handHasLeadSuit;
    }

    public static string ResolveTrickWinner(IReadOnlyList<TrickPlay> trickPlays, Suit? trumpSuit)
    {
        if (trickPlays.Count == 0)
        {
            throw new InvalidOperationException("Cannot resolve an empty trick.");
        }

        var lastWizard = trickPlays.LastOrDefault(x => x.Card.Kind == CardKind.Wizard);
        if (lastWizard is not null)
        {
            return lastWizard.PlayerId;
        }

        if (trickPlays.All(x => x.Card.Kind == CardKind.Jester))
        {
            return trickPlays[0].PlayerId;
        }

        var standardCards = trickPlays.Where(x => x.Card.Kind == CardKind.Standard).ToList();
        if (standardCards.Count == 0)
        {
            return trickPlays[0].PlayerId;
        }

        if (trumpSuit is not null)
        {
            var trumpCards = standardCards.Where(x => x.Card.Suit == trumpSuit).ToList();
            if (trumpCards.Count > 0)
            {
                return trumpCards.MaxBy(x => x.Card.Value)!.PlayerId;
            }
        }

        var leadSuit = LeadSuit(trickPlays);
        if (leadSuit is null)
        {
            return standardCards.MaxBy(x => x.Card.Value)!.PlayerId;
        }

        return standardCards
            .Where(x => x.Card.Suit == leadSuit)
            .MaxBy(x => x.Card.Value)!.PlayerId;
    }

    public static int ScoreRound(int bid, int tricksWon)
    {
        return bid == tricksWon
            ? 10 + (10 * bid)
            : -10 * Math.Abs(bid - tricksWon);
    }

    public static Suit? LeadSuit(IReadOnlyList<TrickPlay> trickPlays)
    {
        return trickPlays
            .Select(x => x.Card)
            .FirstOrDefault(c => c.Kind == CardKind.Standard)?.Suit;
    }
}

public sealed class WizardGameEngine
{
    private readonly Random _random;

    public WizardGameEngine(Random? random = null)
    {
        _random = random ?? new Random();
    }

    public void StartGame(LobbyState state, string requesterPlayerId)
    {
        if (state.Status != LobbyStatus.Lobby)
        {
            throw new InvalidOperationException("Game already started.");
        }

        if (state.Players.Count < 3 || state.Players.Count > 6)
        {
            throw new InvalidOperationException("Game requires 3-6 players.");
        }

        var host = state.Players.SingleOrDefault(p => p.IsHost);
        if (host is null || host.PlayerId != requesterPlayerId)
        {
            throw new InvalidOperationException("Only host can start the game.");
        }

        state.MaxRounds = WizardRules.MaxRounds(state.Players.Count);
        state.RoundHistory.Clear();
        BeginRound(state, 1);
    }

    public void ChooseTrump(LobbyState state, string requesterPlayerId, Suit trumpSuit)
    {
        if (state.Status != LobbyStatus.ChoosingTrump || state.Round is null)
        {
            throw new InvalidOperationException("Trump choice is not pending.");
        }

        var dealer = state.Players.Single(p => p.SeatIndex == state.Round.DealerSeatIndex);
        if (dealer.PlayerId != requesterPlayerId)
        {
            throw new InvalidOperationException("Only dealer can choose trump.");
        }

        state.Round.TrumpSuit = trumpSuit;
        state.Round.RequiresDealerTrumpSelection = false;
        state.Status = LobbyStatus.Bidding;
    }

    public void SubmitBid(LobbyState state, string requesterPlayerId, int bid)
    {
        if (state.Round is null)
        {
            throw new InvalidOperationException("No active round.");
        }

        if (state.Status != LobbyStatus.Bidding)
        {
            throw new InvalidOperationException("Bidding is not active.");
        }

        if (state.Round.CurrentTurnPlayerId != requesterPlayerId)
        {
            throw new InvalidOperationException("Not your turn to bid.");
        }

        var playersInBidOrder = SeatOrder(state.Players, state.Round.StartingSeatIndex);
        if (!WizardRules.IsBidAllowed(state.Round.BidsByPlayer, playersInBidOrder, state.Round.RoundNumber, requesterPlayerId, bid))
        {
            throw new InvalidOperationException("Bid is not allowed by round constraints.");
        }

        state.Round.BidsByPlayer[requesterPlayerId] = bid;
        state.Players.Single(p => p.PlayerId == requesterPlayerId).CurrentBid = bid;
        state.RoundHistory
            .Single(x => x.RoundNumber == state.Round.RoundNumber)
            .BidsByPlayer[requesterPlayerId] = bid;

        var nextBidder = playersInBidOrder.FirstOrDefault(
            p => !state.Round.BidsByPlayer.TryGetValue(p.PlayerId, out var maybeBid) || maybeBid is null);

        if (nextBidder is not null)
        {
            state.Round.CurrentTurnPlayerId = nextBidder.PlayerId;
            return;
        }

        var trickLeader = state.Players.Single(p => p.SeatIndex == state.Round.StartingSeatIndex);
        state.Round.CurrentTurnPlayerId = trickLeader.PlayerId;
        state.Round.CurrentTrick = new TrickState
        {
            TrickNumber = 1,
            LeaderPlayerId = trickLeader.PlayerId
        };
        state.Status = LobbyStatus.Playing;
    }

    public void PlayCard(LobbyState state, string requesterPlayerId, string cardId)
    {
        if (state.Round is null)
        {
            throw new InvalidOperationException("No active round.");
        }

        if (state.Status != LobbyStatus.Playing)
        {
            throw new InvalidOperationException("Round is not in play.");
        }

        if (state.Round.CurrentTurnPlayerId != requesterPlayerId)
        {
            throw new InvalidOperationException("Not your turn to play.");
        }

        var hand = state.Round.HandsByPlayer[requesterPlayerId];
        var card = hand.FirstOrDefault(c => c.Id == cardId)
            ?? throw new InvalidOperationException("Card is not in your hand.");

        if (!WizardRules.IsLegalCardPlay(hand, card, state.Round.CurrentTrick.Plays))
        {
            throw new InvalidOperationException("Card violates follow-suit rule.");
        }

        var player = state.Players.Single(p => p.PlayerId == requesterPlayerId);
        state.Round.CurrentTrick.Plays.Add(new TrickPlay
        {
            PlayerId = requesterPlayerId,
            SeatIndex = player.SeatIndex,
            Card = card
        });
        hand.Remove(card);

        if (state.Round.CurrentTrick.Plays.Count < state.Players.Count)
        {
            state.Round.CurrentTurnPlayerId = NextSeatPlayerId(state.Players, player.SeatIndex);
            return;
        }

        var winnerPlayerId = WizardRules.ResolveTrickWinner(state.Round.CurrentTrick.Plays, state.Round.TrumpSuit);
        state.Round.TricksWonByPlayer[winnerPlayerId]++;
        state.Players.Single(p => p.PlayerId == winnerPlayerId).TricksWonThisRound++;
        state.Round.CompletedTricks++;

        if (state.Round.CompletedTricks >= state.Round.RoundNumber)
        {
            ScoreCurrentRound(state);

            if (state.Round.RoundNumber >= state.MaxRounds)
            {
                state.Status = LobbyStatus.Completed;
                return;
            }

            BeginRound(state, state.Round.RoundNumber + 1);
            return;
        }

        state.Round.CurrentTrick = new TrickState
        {
            TrickNumber = state.Round.CurrentTrick.TrickNumber + 1,
            LeaderPlayerId = winnerPlayerId
        };
        state.Round.CurrentTurnPlayerId = winnerPlayerId;
    }

    private void ScoreCurrentRound(LobbyState state)
    {
        if (state.Round is null)
        {
            return;
        }

        var roundHistory = state.RoundHistory.Single(x => x.RoundNumber == state.Round.RoundNumber);

        foreach (var player in state.Players)
        {
            var bid = state.Round.BidsByPlayer[player.PlayerId]
                ?? throw new InvalidOperationException("Missing bid at score time.");
            var tricksWon = state.Round.TricksWonByPlayer[player.PlayerId];
            player.Score += WizardRules.ScoreRound(bid, tricksWon);
            roundHistory.TotalScoresByPlayer[player.PlayerId] = player.Score;
        }

        roundHistory.IsCompleted = true;
    }

    private void BeginRound(LobbyState state, int roundNumber)
    {
        var playerCount = state.Players.Count;
        var startingSeat = (roundNumber - 1) % playerCount;
        var dealerSeat = (startingSeat - 1 + playerCount) % playerCount;
        var round = new RoundState
        {
            RoundNumber = roundNumber,
            StartingSeatIndex = startingSeat,
            DealerSeatIndex = dealerSeat,
            CurrentTurnPlayerId = state.Players.Single(p => p.SeatIndex == startingSeat).PlayerId,
            CurrentTrick = new TrickState
            {
                TrickNumber = 1,
                LeaderPlayerId = state.Players.Single(p => p.SeatIndex == startingSeat).PlayerId
            }
        };

        var historyEntry = new RoundHistoryEntry
        {
            RoundNumber = roundNumber
        };

        foreach (var player in state.Players)
        {
            player.CurrentBid = null;
            player.TricksWonThisRound = 0;
            round.BidsByPlayer[player.PlayerId] = null;
            round.TricksWonByPlayer[player.PlayerId] = 0;
            round.HandsByPlayer[player.PlayerId] = [];
            historyEntry.BidsByPlayer[player.PlayerId] = null;
            historyEntry.TotalScoresByPlayer[player.PlayerId] = null;
        }

        var deck = DeckFactory.CreateShuffledDeck(_random);
        var dealingOrder = SeatOrder(state.Players, startingSeat);

        for (var i = 0; i < roundNumber; i++)
        {
            foreach (var player in dealingOrder)
            {
                round.HandsByPlayer[player.PlayerId].Add(deck[0]);
                deck.RemoveAt(0);
            }
        }

        round.UpCard = deck.Count > 0 ? deck[0] : null;
        round.TrumpSuit = ResolveTrump(round.UpCard);
        round.RequiresDealerTrumpSelection = round.UpCard?.Kind == CardKind.Wizard;

        state.Round = round;
        state.RoundHistory.Add(historyEntry);
        state.Status = round.RequiresDealerTrumpSelection ? LobbyStatus.ChoosingTrump : LobbyStatus.Bidding;
    }

    private static Suit? ResolveTrump(Card? upCard)
    {
        if (upCard is null)
        {
            return null;
        }

        return upCard.Kind switch
        {
            CardKind.Standard => upCard.Suit,
            CardKind.Jester => null,
            CardKind.Wizard => null,
            _ => null
        };
    }

    private static IReadOnlyList<PlayerState> SeatOrder(IReadOnlyList<PlayerState> players, int startSeatIndex)
    {
        var count = players.Count;
        var sorted = players.OrderBy(p => p.SeatIndex).ToList();
        var result = new List<PlayerState>(count);
        for (var i = 0; i < count; i++)
        {
            var index = (startSeatIndex + i) % count;
            result.Add(sorted[index]);
        }

        return new ReadOnlyCollection<PlayerState>(result);
    }

    private static string NextSeatPlayerId(IReadOnlyList<PlayerState> players, int currentSeat)
    {
        var count = players.Count;
        var nextSeat = (currentSeat + 1) % count;
        return players.Single(p => p.SeatIndex == nextSeat).PlayerId;
    }
}
