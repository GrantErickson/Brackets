namespace Brackets.Core.Models;

/// <summary>
/// Identifies the occupant of a game slot. For first-round winners-bracket games the occupant
/// is a concrete team number (<see cref="SlotKind.Seed"/>). Everywhere else the occupant is not
/// known until earlier games are played, so the slot references the feeding game by number
/// (<see cref="SlotKind.WinnerOf"/> / <see cref="SlotKind.LoserOf"/>).
/// </summary>
public sealed class SlotRef
{
    public SlotKind Kind { get; init; }

    /// <summary>Team/seed number when <see cref="Kind"/> is <see cref="SlotKind.Seed"/>, otherwise the source game number.</summary>
    public int Ref { get; init; }

    public static SlotRef Seed(int seedNumber) => new() { Kind = SlotKind.Seed, Ref = seedNumber };

    public static SlotRef WinnerOf(int gameNumber) => new() { Kind = SlotKind.WinnerOf, Ref = gameNumber };

    public static SlotRef LoserOf(int gameNumber) => new() { Kind = SlotKind.LoserOf, Ref = gameNumber };

    public override string ToString() => Kind switch
    {
        SlotKind.Seed => $"#{Ref}",
        SlotKind.WinnerOf => $"W{Ref}",
        SlotKind.LoserOf => $"L{Ref}",
        _ => "?",
    };
}
