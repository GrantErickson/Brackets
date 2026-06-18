using Brackets.Core.Models;

namespace Brackets.Core.Validation;

/// <summary>Validates that a generated bracket satisfies the structural rules the library guarantees.</summary>
public static class BracketValidator
{
    public const int MinGamesPerTeam = 3;

    /// <summary>Returns the list of validation problems; an empty list means the bracket is valid.</summary>
    public static IReadOnlyList<string> Validate(Bracket bracket)
    {
        var errors = new List<string>();
        var byNumber = bracket.Games.ToDictionary(g => g.GameNumber);

        ValidateReferences(bracket, byNumber, errors);
        ValidateSingleChampion(bracket, errors);
        ValidateForwardPointers(bracket, byNumber, errors);
        ValidateSequencing(bracket, byNumber, errors);
        ValidateMinimumGames(bracket, byNumber, errors);
        ValidateByes(bracket, errors);

        return errors;
    }

    public static void ThrowIfInvalid(Bracket bracket)
    {
        var errors = Validate(bracket);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Invalid bracket:\n - " + string.Join("\n - ", errors));
        }
    }

    private static void ValidateReferences(Bracket bracket, Dictionary<int, Game> byNumber, List<string> errors)
    {
        foreach (var game in bracket.Games)
        {
            foreach (var (slot, label) in new[] { (game.Team1, "team1"), (game.Team2, "team2") })
            {
                switch (slot.Kind)
                {
                    case SlotKind.Seed:
                        if (slot.Ref < 1 || slot.Ref > bracket.TeamCount)
                        {
                            errors.Add($"Game {game.GameNumber} {label} references seed {slot.Ref} outside 1..{bracket.TeamCount}.");
                        }

                        break;
                    case SlotKind.WinnerOf:
                    case SlotKind.LoserOf:
                        if (!byNumber.ContainsKey(slot.Ref))
                        {
                            errors.Add($"Game {game.GameNumber} {label} references missing game {slot.Ref}.");
                        }
                        else if (slot.Ref >= game.GameNumber)
                        {
                            errors.Add($"Game {game.GameNumber} {label} references game {slot.Ref} which is not earlier.");
                        }

                        break;
                }
            }
        }
    }

    private static void ValidateSingleChampion(Bracket bracket, List<string> errors)
    {
        int champions = bracket.Games.Count(g => g.WinnerNextGame is null);
        if (champions != 1)
        {
            errors.Add($"Expected exactly one championship game (winnerNextGame == null) but found {champions}.");
        }
    }

    private static void ValidateForwardPointers(Bracket bracket, Dictionary<int, Game> byNumber, List<string> errors)
    {
        // Every slot fed by another game must be the unique target of that game's matching forward pointer.
        var filledBy = new Dictionary<(int Game, int Slot), int>();

        foreach (var game in bracket.Games)
        {
            CheckPointer(game, game.WinnerNextGame, game.WinnerNextSlot, SlotKind.WinnerOf, "winner");
            CheckPointer(game, game.LoserNextGame, game.LoserNextSlot, SlotKind.LoserOf, "loser");
        }

        void CheckPointer(Game game, int? nextGame, int? nextSlot, SlotKind expectedKind, string label)
        {
            if (nextGame is null)
            {
                return;
            }

            if (nextSlot is not (1 or 2))
            {
                errors.Add($"Game {game.GameNumber} {label}NextSlot must be 1 or 2.");
                return;
            }

            if (!byNumber.TryGetValue(nextGame.Value, out var target))
            {
                errors.Add($"Game {game.GameNumber} {label} points to missing game {nextGame}.");
                return;
            }

            var key = (target.GameNumber, nextSlot.Value);
            if (filledBy.TryGetValue(key, out int other))
            {
                errors.Add($"Slot {nextSlot} of game {target.GameNumber} is filled by both game {other} and game {game.GameNumber}.");
            }
            else
            {
                filledBy[key] = game.GameNumber;
            }

            var slotRef = nextSlot == 1 ? target.Team1 : target.Team2;
            if (slotRef.Kind != expectedKind || slotRef.Ref != game.GameNumber)
            {
                errors.Add($"Game {game.GameNumber} {label} pointer to game {target.GameNumber} slot {nextSlot} does not match that slot's reference {slotRef}.");
            }
        }
    }

    private static void ValidateSequencing(Bracket bracket, Dictionary<int, Game> byNumber, List<string> errors)
    {
        foreach (var game in bracket.Games)
        {
            foreach (var slot in new[] { game.Team1, game.Team2 })
            {
                if (slot.Kind is SlotKind.WinnerOf or SlotKind.LoserOf
                    && byNumber.TryGetValue(slot.Ref, out var feeder)
                    && game.SequenceNumber <= feeder.SequenceNumber)
                {
                    errors.Add($"Game {game.GameNumber} (seq {game.SequenceNumber}) is not after feeder game {feeder.GameNumber} (seq {feeder.SequenceNumber}).");
                }
            }
        }
    }

    private static void ValidateMinimumGames(Bracket bracket, Dictionary<int, Game> byNumber, List<string> errors)
    {
        // Each seed enters at exactly one game (a Seed slot). Following the loser pointer repeatedly (i.e.
        // losing every game) yields the fewest games the team can play before elimination.
        var entryGame = new Dictionary<int, Game>();
        foreach (var game in bracket.Games)
        {
            foreach (var slot in new[] { game.Team1, game.Team2 })
            {
                if (slot.Kind == SlotKind.Seed)
                {
                    if (entryGame.ContainsKey(slot.Ref))
                    {
                        errors.Add($"Seed {slot.Ref} enters in more than one game.");
                    }
                    else
                    {
                        entryGame[slot.Ref] = game;
                    }
                }
            }
        }

        for (int seed = 1; seed <= bracket.TeamCount; seed++)
        {
            if (!entryGame.TryGetValue(seed, out var game))
            {
                errors.Add($"Seed {seed} never enters the bracket.");
                continue;
            }

            int count = 0;
            var current = game;
            while (current is not null)
            {
                count++;
                current = current.LoserNextGame is int next ? byNumber[next] : null;
            }

            if (count < MinGamesPerTeam)
            {
                errors.Add($"Seed {seed} can be eliminated after only {count} game(s); minimum is {MinGamesPerTeam}.");
            }
        }
    }

    private static void ValidateByes(Bracket bracket, List<string> errors)
    {
        var seen = new HashSet<int>();
        foreach (var bye in bracket.Byes)
        {
            if (bye.Slot.Kind != SlotKind.Seed)
            {
                errors.Add($"Bye in {bye.Deck} round {bye.Round} is not tied to a concrete seed ({bye.Slot}).");
                continue;
            }

            if (!seen.Add(bye.Slot.Ref))
            {
                errors.Add($"Seed {bye.Slot.Ref} has more than one bye.");
            }
        }
    }
}
