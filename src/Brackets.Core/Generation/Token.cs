using Brackets.Core.Models;

namespace Brackets.Core.Generation;

/// <summary>
/// A future participant flowing through deck construction. A token references where its occupant
/// comes from (a seed, or the winner/loser of a game) and tracks scheduling/bye bookkeeping.
/// </summary>
internal sealed class Token
{
    public SlotRef Ref { get; init; } = SlotRef.Seed(0);

    /// <summary>Earliest provisional sequence at which this participant is known / available to play.</summary>
    public int AvailableSeq { get; set; }

    /// <summary>How many games this participant's lineage has played within the current deck (0 for a fresh drop-in).</summary>
    public int DeckDepth { get; set; }

    /// <summary>
    /// Conservative flag: true if any team that could occupy this token may already have taken a bye.
    /// Used so a team never receives a second bye.
    /// </summary>
    public bool MayHaveByed { get; set; }
}
