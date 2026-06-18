using Brackets.Core.Generation;
using Brackets.Core.Models;
using Brackets.Core.Simulation;
using Xunit;

namespace Brackets.Tests;

public class SimulationTests
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
    public void Random_playouts_yield_one_champion_with_no_clashes_and_three_game_minimum(int n)
    {
        var bracket = Generate(n);

        for (int trial = 0; trial < 200; trial++)
        {
            var rng = new Random(trial * 7919 + n);
            var result = Simulator.Run(bracket, (_, a, b) => rng.Next(2) == 0 ? a : b);

            Assert.InRange(result.Champion, 1, n);

            // Every team plays at least three games.
            for (int seed = 1; seed <= n; seed++)
            {
                Assert.True(result.GamesPlayedBySeed.GetValueOrDefault(seed) >= 3,
                    $"Seed {seed} played {result.GamesPlayedBySeed.GetValueOrDefault(seed)} games (trial {trial}, n={n}).");
            }

            // No team appears in two games of the same sequence (synchronous play is feasible).
            foreach (var group in result.Appearances.GroupBy(a => a.Sequence))
            {
                var seeds = group.Select(a => a.Seed).ToList();
                Assert.Equal(seeds.Count, seeds.Distinct().Count());
            }

            // No team ever takes more than one bye.
            Assert.All(result.ByesBySeed.Values, c => Assert.True(c <= 1));
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Any_team_can_win_the_championship(int n)
    {
        var bracket = Generate(n);
        for (int seed = 1; seed <= n; seed++)
        {
            int favourite = seed;
            var result = Simulator.Run(bracket, (_, a, b) => a == favourite ? a : b == favourite ? b : a);
            Assert.Equal(favourite, result.Champion);
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void A_team_that_loses_its_first_two_games_can_still_win(int n)
    {
        // Pick a seed that starts in the winners bracket round 1, force it to lose its first game,
        // then win everything afterwards: it must be able to climb back and take the title.
        var bracket = Generate(n);
        var firstGame = bracket.Games
            .Where(g => g.Deck == Deck.Winners && g.Team1.Kind == SlotKind.Seed && g.Team2.Kind == SlotKind.Seed)
            .OrderBy(g => g.GameNumber)
            .First();
        int comebackSeed = firstGame.Team1.Ref;

        int comebackGames = 0;
        var result = Simulator.Run(bracket, (game, a, b) =>
        {
            bool involved = a == comebackSeed || b == comebackSeed;
            if (!involved)
            {
                return a;
            }

            comebackGames++;
            if (comebackGames <= 2)
            {
                return a == comebackSeed ? b : a; // lose the first two games (drop to the 2-loss deck)
            }

            return comebackSeed; // win everything afterwards and climb all the way back
        });

        Assert.Equal(comebackSeed, result.Champion);
        Assert.True(result.GamesPlayedBySeed[comebackSeed] >= 3);
    }
}
