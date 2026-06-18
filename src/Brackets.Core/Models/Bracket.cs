namespace Brackets.Core.Models;

/// <summary>Records a bye: a team that advances a round without playing because it had no opponent.</summary>
public sealed class Bye
{
    public Deck Deck { get; set; }

    public int Round { get; set; }

    /// <summary>
    /// The slot that advances for free. For winners-bracket first-round byes this is a concrete
    /// <see cref="SlotKind.Seed"/>; for lower-deck byes it is the feeding game reference (the actual
    /// team depends on how earlier games turn out).
    /// </summary>
    public SlotRef Slot { get; set; } = SlotRef.Seed(0);
}

/// <summary>
/// A fully generated three-life elimination bracket for <see cref="TeamCount"/> teams.
/// Teams are identified by seed number 1..N. <see cref="TeamNames"/> optionally maps a seed to a
/// display name (used by the PDF renderer); it never affects bracket identity.
/// </summary>
public sealed class Bracket
{
    public int TeamCount { get; set; }

    public string Format { get; set; } = "three-life";

    public List<Game> Games { get; set; } = new();

    public List<Bye> Byes { get; set; } = new();

    /// <summary>Optional seed -> display name map (seed numbers are the canonical identity).</summary>
    public Dictionary<int, string> TeamNames { get; set; } = new();

    /// <summary>The single game whose winner is the champion (its <see cref="Game.WinnerNextGame"/> is null).</summary>
    public Game ChampionshipGame => Games.Single(g => g.WinnerNextGame is null);

    /// <summary>Number of distinct synchronous time slots.</summary>
    public int SequenceCount => Games.Count == 0 ? 0 : Games.Max(g => g.SequenceNumber);

    public Game GameByNumber(int number) => Games.Single(g => g.GameNumber == number);
}
