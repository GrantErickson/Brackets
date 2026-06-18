namespace Brackets.Core.Models;

/// <summary>
/// A single game in the bracket. The progression is fully described by
/// <see cref="WinnerNextGame"/> / <see cref="LoserNextGame"/> (and the matching slot numbers),
/// so the whole tournament is a directed graph of games.
/// </summary>
public sealed class Game
{
    /// <summary>Unique 1-based identifier for the game ("number").</summary>
    public int GameNumber { get; set; }

    /// <summary>
    /// Synchronous round / time slot. All games sharing a sequence number are played at the same
    /// time, so a real clock time can be assigned per sequence number.
    /// </summary>
    public int SequenceNumber { get; set; }

    public Deck Deck { get; set; }

    /// <summary>1-based round number within the game's deck (informational; aids layout).</summary>
    public int Round { get; set; }

    public SlotRef Team1 { get; set; } = SlotRef.Seed(0);

    public SlotRef Team2 { get; set; } = SlotRef.Seed(0);

    /// <summary>Game number the winner advances to, or <c>null</c> if this game crowns the champion.</summary>
    public int? WinnerNextGame { get; set; }

    /// <summary>Slot (1 or 2) the winner fills in <see cref="WinnerNextGame"/>.</summary>
    public int? WinnerNextSlot { get; set; }

    /// <summary>Game number the loser drops to, or <c>null</c> if the loser is eliminated.</summary>
    public int? LoserNextGame { get; set; }

    /// <summary>Slot (1 or 2) the loser fills in <see cref="LoserNextGame"/>.</summary>
    public int? LoserNextSlot { get; set; }

    /// <summary>True for conditional games (grand-final resets) that are only played when required.</summary>
    public bool IfNecessary { get; set; }

    /// <summary>Optional human-readable note (e.g. the condition under which a reset game is played).</summary>
    public string? Note { get; set; }
}
