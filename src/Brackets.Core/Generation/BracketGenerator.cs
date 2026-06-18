using Brackets.Core.Models;

namespace Brackets.Core.Generation;

/// <summary>
/// Generates a three-life elimination bracket. Teams cascade through three decks by loss count:
/// Winners (0 losses) -> LowerOne (1 loss) -> LowerTwo (2 losses) -> eliminated (3rd loss). Because a
/// team must lose three times to be eliminated, every team is guaranteed at least three games, and a
/// team that has lost twice can still climb back to win the championship.
/// </summary>
public static class BracketGenerator
{
    public static Bracket Generate(BracketOptions options)
    {
        options.Validate();

        var ctx = new GenerationContext();
        int n = options.TeamCount;

        var (winnersChamp, winnersLosers) = ctx.BuildWinners(n);
        var (lowerOneChamp, lowerOneLosers) = ctx.BuildLowerDeck(Deck.LowerOne, winnersLosers, eliminateLosers: false);
        var (lowerTwoChamp, _) = ctx.BuildLowerDeck(Deck.LowerTwo, lowerOneLosers, eliminateLosers: true);
        ctx.BuildFinals(winnersChamp, lowerOneChamp, lowerTwoChamp);

        ctx.AssignSequenceNumbers();

        return new Bracket
        {
            TeamCount = n,
            Format = "three-life",
            Games = ctx.Games,
            Byes = ctx.Byes,
            TeamNames = options.TeamNames is null
                ? new Dictionary<int, string>()
                : new Dictionary<int, string>(options.TeamNames),
        };
    }
}

/// <summary>Mutable state shared across the deck builders while a single bracket is assembled.</summary>
internal sealed class GenerationContext
{
    public List<Game> Games { get; } = new();

    public List<Bye> Byes { get; } = new();

    private int _nextGameNumber = 1;

    private Game NewGame(Deck deck, int round)
    {
        var game = new Game { GameNumber = _nextGameNumber++, Deck = deck, Round = round };
        Games.Add(game);
        return game;
    }

    /// <summary>Places <paramref name="token"/> into the given slot of <paramref name="game"/> and wires the source game's forward pointer.</summary>
    private void WireSlot(Game game, int slot, Token token)
    {
        if (slot == 1)
        {
            game.Team1 = token.Ref;
        }
        else
        {
            game.Team2 = token.Ref;
        }

        switch (token.Ref.Kind)
        {
            case SlotKind.WinnerOf:
                var ws = GameByNumber(token.Ref.Ref);
                ws.WinnerNextGame = game.GameNumber;
                ws.WinnerNextSlot = slot;
                break;
            case SlotKind.LoserOf:
                var ls = GameByNumber(token.Ref.Ref);
                ls.LoserNextGame = game.GameNumber;
                ls.LoserNextSlot = slot;
                break;
            // Seed: an entry point, no source pointer to wire.
        }
    }

    private Game GameByNumber(int number) => Games[number - 1];

    private static Token WinnerToken(Game game, int availableSeq, bool mayHaveByed) => new()
    {
        Ref = SlotRef.WinnerOf(game.GameNumber),
        AvailableSeq = availableSeq,
        MayHaveByed = mayHaveByed,
    };

    private static Token LoserToken(Game game, int availableSeq, bool mayHaveByed) => new()
    {
        Ref = SlotRef.LoserOf(game.GameNumber),
        AvailableSeq = availableSeq,
        MayHaveByed = mayHaveByed,
    };

    private void RecordBye(Deck deck, int round, SlotRef slot) =>
        Byes.Add(new Bye { Deck = deck, Round = round, Slot = slot });

    // ---- Winners deck: standard seeded single elimination with byes for the top seeds ----

    public (Token Champion, List<Token> Losers) BuildWinners(int n)
    {
        int p = Seeding.NextPowerOfTwo(n);
        int[] order = Seeding.SeedOrder(p);
        var losers = new List<Token>();
        var current = new List<Token>();
        int round = 1;

        for (int i = 0; i < p; i += 2)
        {
            int seedA = order[i];
            int seedB = order[i + 1];
            bool aReal = seedA <= n;
            bool bReal = seedB <= n;

            if (aReal && bReal)
            {
                var game = NewGame(Deck.Winners, round);
                WireSlot(game, 1, new Token { Ref = SlotRef.Seed(seedA) });
                WireSlot(game, 2, new Token { Ref = SlotRef.Seed(seedB) });
                current.Add(WinnerToken(game, round + 1, mayHaveByed: false));
                losers.Add(LoserToken(game, round + 1, mayHaveByed: false));
            }
            else if (aReal || bReal)
            {
                int seed = aReal ? seedA : seedB;
                RecordBye(Deck.Winners, round, SlotRef.Seed(seed));
                current.Add(new Token { Ref = SlotRef.Seed(seed), AvailableSeq = round + 1, MayHaveByed = true });
            }
            else
            {
                throw new InvalidOperationException("Two byes in a single first-round matchup; unexpected for 8..16 teams.");
            }
        }

        while (current.Count > 1)
        {
            round++;
            var next = new List<Token>();
            for (int i = 0; i < current.Count; i += 2)
            {
                var t1 = current[i];
                var t2 = current[i + 1];
                var game = NewGame(Deck.Winners, round);
                WireSlot(game, 1, t1);
                WireSlot(game, 2, t2);
                bool mb = t1.MayHaveByed || t2.MayHaveByed;
                next.Add(WinnerToken(game, round + 1, mb));
                losers.Add(LoserToken(game, round + 1, mb));
            }

            current = next;
        }

        return (current[0], losers);
    }

    // ---- Lower decks: single elimination consuming a staggered stream of drop-ins ----

    /// <summary>
    /// Builds a lower deck that consumes <paramref name="entrants"/> (drop-ins arriving over time) into a
    /// single champion by repeatedly pairing the two earliest-available participants. This forms a proper
    /// binary tree (every game has exactly two inputs), so a participant never sits out a round it had
    /// already entered — lower decks contain no byes. Each game's loser drops to the next deck (returned in
    /// <c>Losers</c>) or, when <paramref name="eliminateLosers"/> is true, is eliminated (their third loss).
    /// </summary>
    public (Token Champion, List<Token> Losers) BuildLowerDeck(Deck deck, List<Token> entrants, bool eliminateLosers)
    {
        var losers = new List<Token>();
        var available = new List<Token>();
        foreach (var entrant in entrants)
        {
            entrant.DeckDepth = 0; // fresh entry into this deck
            available.Add(entrant);
        }

        while (available.Count > 1)
        {
            // Pair the two earliest-available participants (ties broken by source game order for determinism).
            available.Sort((a, b) => a.AvailableSeq != b.AvailableSeq
                ? a.AvailableSeq - b.AvailableSeq
                : a.Ref.Ref - b.Ref.Ref);

            var t1 = available[0];
            var t2 = available[1];
            available.RemoveRange(0, 2);

            int round = Math.Max(t1.DeckDepth, t2.DeckDepth) + 1;
            int seq = Math.Max(t1.AvailableSeq, t2.AvailableSeq);
            var game = NewGame(deck, round);
            WireSlot(game, 1, t1);
            WireSlot(game, 2, t2);
            bool mb = t1.MayHaveByed || t2.MayHaveByed;

            available.Add(new Token
            {
                Ref = SlotRef.WinnerOf(game.GameNumber),
                AvailableSeq = seq + 1,
                DeckDepth = round,
                MayHaveByed = mb,
            });

            if (!eliminateLosers)
            {
                losers.Add(LoserToken(game, seq + 1, mb));
            }
        }

        return (available[0], losers);
    }

    // ---- Finals: converge the three deck champions into a single champion ----

    public void BuildFinals(Token winnersChamp, Token lowerOneChamp, Token lowerTwoChamp)
    {
        // Semifinal: 1-loss champion vs 2-loss champion. The loser is out.
        var semi = NewGame(Deck.Finals, 1);
        WireSlot(semi, 1, lowerOneChamp);
        WireSlot(semi, 2, lowerTwoChamp);
        semi.Note = "Lower final: 1-loss champion vs 2-loss champion. Loser is eliminated.";

        // Grand final: undefeated winners-deck champion vs the comeback challenger.
        var grandFinal = NewGame(Deck.Finals, 2);
        WireSlot(grandFinal, 1, winnersChamp);
        WireSlot(grandFinal, 2, WinnerToken(semi, 0, false));
        grandFinal.Note = "Grand final: undefeated winners-deck champion vs comeback challenger.";

        // Reset: only played if the challenger wins the grand final (the winners champion's first loss).
        var reset = NewGame(Deck.Finals, 3);
        reset.IfNecessary = true;
        WireSlot(reset, 1, WinnerToken(grandFinal, 0, false));
        WireSlot(reset, 2, LoserToken(grandFinal, 0, false));
        reset.Note = "Reset — played only if the challenger won the grand final, forcing a deciding game.";
    }

    // ---- Sequence numbers: earliest synchronous schedule via longest-path layering ----

    public void AssignSequenceNumbers()
    {
        var memo = new Dictionary<int, int>();
        foreach (var game in Games)
        {
            game.SequenceNumber = SequenceOf(game, memo);
        }
    }

    private int SequenceOf(Game game, Dictionary<int, int> memo)
    {
        if (memo.TryGetValue(game.GameNumber, out int cached))
        {
            return cached;
        }

        int seq = 1;
        foreach (var slot in new[] { game.Team1, game.Team2 })
        {
            if (slot.Kind is SlotKind.WinnerOf or SlotKind.LoserOf)
            {
                seq = Math.Max(seq, SequenceOf(GameByNumber(slot.Ref), memo) + 1);
            }
        }

        memo[game.GameNumber] = seq;
        return seq;
    }
}
