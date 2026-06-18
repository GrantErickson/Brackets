namespace Brackets.Core.Models;

/// <summary>
/// Describes where the team that occupies a game slot comes from.
/// </summary>
public enum SlotKind
{
    /// <summary>A concrete team / seed number (only used for first-round winners-bracket slots).</summary>
    Seed,

    /// <summary>The winner of another game (referenced by its game number).</summary>
    WinnerOf,

    /// <summary>The loser of another game (referenced by its game number).</summary>
    LoserOf,
}

/// <summary>
/// Which "deck" of the three-life cascade a game belongs to.
/// A team is eliminated only on its third loss, cascading W -> B1 -> B2 -> out.
/// </summary>
public enum Deck
{
    /// <summary>Winners deck: teams with zero losses.</summary>
    Winners,

    /// <summary>First lower deck: teams with exactly one loss.</summary>
    LowerOne,

    /// <summary>Second lower deck: teams with exactly two losses. A loss here is elimination.</summary>
    LowerTwo,

    /// <summary>Finals: the three deck champions converge into a single champion.</summary>
    Finals,
}
