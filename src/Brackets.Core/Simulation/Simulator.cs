using Brackets.Core.Models;

namespace Brackets.Core.Simulation;

/// <summary>Outcome of playing a generated bracket to completion.</summary>
public sealed class SimulationResult
{
    /// <summary>Seed number of the tournament champion.</summary>
    public int Champion { get; init; }

    /// <summary>Number of games each seed actually played (byes excluded).</summary>
    public IReadOnlyDictionary<int, int> GamesPlayedBySeed { get; init; } = new Dictionary<int, int>();

    /// <summary>Number of byes each seed received.</summary>
    public IReadOnlyDictionary<int, int> ByesBySeed { get; init; } = new Dictionary<int, int>();

    /// <summary>For each played game, the (sequence, seed) pairs actually on the field, for clash checks.</summary>
    public IReadOnlyList<(int Sequence, int Seed)> Appearances { get; init; } = new List<(int, int)>();

    /// <summary>Game numbers that were actually played (an "if necessary" reset may be skipped).</summary>
    public IReadOnlySet<int> PlayedGames { get; init; } = new HashSet<int>();
}

/// <summary>
/// Plays a generated <see cref="Bracket"/> to completion under a caller-supplied winner-picking rule.
/// Handles the conditional grand-final reset (only played when the challenger wins the grand final).
/// </summary>
public static class Simulator
{
    /// <summary>
    /// Resolves <paramref name="bracket"/> using <paramref name="pickWinner"/>, which receives the game and
    /// the two resolved seed numbers and returns the seed that wins.
    /// </summary>
    public static SimulationResult Run(Bracket bracket, Func<Game, int, int, int> pickWinner)
    {
        var winnerSeed = new Dictionary<int, int>();
        var loserSeed = new Dictionary<int, int>();
        var winnerSlot = new Dictionary<int, int>();
        var played = new HashSet<int>();
        var gamesPlayed = new Dictionary<int, int>();
        var appearances = new List<(int, int)>();

        var ordered = bracket.Games
            .OrderBy(g => g.SequenceNumber)
            .ThenBy(g => g.GameNumber)
            .ToList();

        foreach (var game in ordered)
        {
            if (game.IfNecessary && !ResetNeeded(game, winnerSlot))
            {
                continue;
            }

            int p1 = ResolveSlot(game.Team1, winnerSeed, loserSeed);
            int p2 = ResolveSlot(game.Team2, winnerSeed, loserSeed);

            int winner = pickWinner(game, p1, p2);
            if (winner != p1 && winner != p2)
            {
                throw new InvalidOperationException($"pickWinner returned {winner}, not a participant of game {game.GameNumber}.");
            }

            int loser = winner == p1 ? p2 : p1;
            winnerSeed[game.GameNumber] = winner;
            loserSeed[game.GameNumber] = loser;
            winnerSlot[game.GameNumber] = winner == p1 ? 1 : 2;
            played.Add(game.GameNumber);

            gamesPlayed[p1] = gamesPlayed.GetValueOrDefault(p1) + 1;
            gamesPlayed[p2] = gamesPlayed.GetValueOrDefault(p2) + 1;
            appearances.Add((game.SequenceNumber, p1));
            appearances.Add((game.SequenceNumber, p2));
        }

        var byes = new Dictionary<int, int>();
        foreach (var bye in bracket.Byes.Where(b => b.Slot.Kind == SlotKind.Seed))
        {
            byes[bye.Slot.Ref] = byes.GetValueOrDefault(bye.Slot.Ref) + 1;
        }

        var champGame = bracket.ChampionshipGame;
        int champion = played.Contains(champGame.GameNumber)
            ? winnerSeed[champGame.GameNumber]
            : winnerSeed[champGame.Team1.Ref]; // reset skipped: champion is the grand-final winner

        return new SimulationResult
        {
            Champion = champion,
            GamesPlayedBySeed = gamesPlayed,
            ByesBySeed = byes,
            Appearances = appearances,
            PlayedGames = played,
        };
    }

    private static bool ResetNeeded(Game reset, Dictionary<int, int> winnerSlot)
    {
        // The reset's slot 1 is the winner of the grand final; it is required only when the grand-final
        // winner came from slot 2 (the challenger beat the undefeated winners-deck champion).
        if (reset.Team1.Kind != SlotKind.WinnerOf)
        {
            return true;
        }

        return winnerSlot.TryGetValue(reset.Team1.Ref, out int slot) && slot == 2;
    }

    private static int ResolveSlot(SlotRef slot, Dictionary<int, int> winnerSeed, Dictionary<int, int> loserSeed) => slot.Kind switch
    {
        SlotKind.Seed => slot.Ref,
        SlotKind.WinnerOf => winnerSeed[slot.Ref],
        SlotKind.LoserOf => loserSeed[slot.Ref],
        _ => throw new InvalidOperationException("Unknown slot kind."),
    };
}
