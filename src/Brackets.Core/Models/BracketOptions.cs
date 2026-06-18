namespace Brackets.Core.Models;

/// <summary>Input options for generating a bracket.</summary>
public sealed class BracketOptions
{
    public const int MinTeams = 8;
    public const int MaxTeams = 16;

    public int TeamCount { get; set; }

    /// <summary>Optional seed -> display name map for rendering.</summary>
    public Dictionary<int, string>? TeamNames { get; set; }

    public void Validate()
    {
        if (TeamCount < MinTeams || TeamCount > MaxTeams)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TeamCount),
                TeamCount,
                $"Team count must be between {MinTeams} and {MaxTeams} (inclusive).");
        }
    }
}
