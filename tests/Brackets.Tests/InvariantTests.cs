using Brackets.Core.Generation;
using Brackets.Core.Models;
using Brackets.Core.Validation;
using Xunit;

namespace Brackets.Tests;

public class InvariantTests
{
    public static IEnumerable<object[]> TeamCounts()
    {
        for (int n = BracketOptions.MinTeams; n <= BracketOptions.MaxTeams; n++)
        {
            yield return new object[] { n };
        }
    }

    private static Bracket Generate(int n) => BracketGenerator.Generate(new BracketOptions { TeamCount = n });

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Bracket_passes_structural_validation(int n)
    {
        var errors = BracketValidator.Validate(Generate(n));
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Total_game_count_matches_three_life_formula(int n)
    {
        // W: N-1 games, B1: N-2, B2: N-3, finals: semi + grand final + reset = 3. Total = 3N-3.
        Assert.Equal(3 * n - 3, Generate(n).Games.Count);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Exactly_one_championship_game(int n)
    {
        Assert.Single(Generate(n).Games, g => g.WinnerNextGame is null);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Every_team_is_guaranteed_at_least_three_games(int n)
    {
        var bracket = Generate(n);
        var byNumber = bracket.Games.ToDictionary(g => g.GameNumber);
        var entry = new Dictionary<int, Game>();
        foreach (var g in bracket.Games)
        {
            foreach (var slot in new[] { g.Team1, g.Team2 })
            {
                if (slot.Kind == SlotKind.Seed)
                {
                    entry[slot.Ref] = g;
                }
            }
        }

        for (int seed = 1; seed <= n; seed++)
        {
            int count = 0;
            Game? cur = entry[seed];
            while (cur is not null)
            {
                count++;
                cur = cur.LoserNextGame is int next ? byNumber[next] : null;
            }

            Assert.True(count >= 3, $"Seed {seed} could be eliminated in {count} games.");
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void At_most_one_bye_per_team_and_only_in_the_winners_deck(int n)
    {
        var bracket = Generate(n);
        Assert.All(bracket.Byes, b =>
        {
            Assert.Equal(Deck.Winners, b.Deck);
            Assert.Equal(SlotKind.Seed, b.Slot.Kind);
        });

        var perSeed = bracket.Byes.GroupBy(b => b.Slot.Ref).Select(grp => grp.Count());
        Assert.All(perSeed, c => Assert.True(c <= 1));
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Bye_count_equals_padding_to_next_power_of_two(int n)
    {
        int p = 1;
        while (p < n)
        {
            p <<= 1;
        }

        Assert.Equal(p - n, Generate(n).Byes.Count);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Sequence_numbers_are_contiguous_from_one(int n)
    {
        var bracket = Generate(n);
        var sequences = bracket.Games.Select(g => g.SequenceNumber).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(Enumerable.Range(1, sequences.Count), sequences);
    }
}
