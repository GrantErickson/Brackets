using System.Text.Json;
using Brackets.Core.Generation;
using Brackets.Core.Json;
using Brackets.Core.Models;
using Xunit;

namespace Brackets.Tests;

public class JsonTests
{
    public static IEnumerable<object[]> TeamCounts()
    {
        for (int n = BracketOptions.MinTeams; n <= BracketOptions.MaxTeams; n++)
        {
            yield return new object[] { n };
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Round_trips_without_loss(int n)
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = n });
        string first = BracketJson.Serialize(bracket);
        string second = BracketJson.Serialize(BracketJson.Deserialize(first));
        Assert.Equal(first, second);
    }

    [Fact]
    public void Uses_the_documented_field_names_and_codes()
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 8 });
        using var doc = JsonDocument.Parse(BracketJson.Serialize(bracket));
        var root = doc.RootElement;

        Assert.Equal(8, root.GetProperty("teamCount").GetInt32());
        Assert.Equal("three-life", root.GetProperty("format").GetString());

        var game = root.GetProperty("games")[0];
        foreach (var field in new[] { "gameNumber", "sequenceNumber", "bracket", "team1", "team2" })
        {
            Assert.True(game.TryGetProperty(field, out _), $"Missing field '{field}'.");
        }

        Assert.Equal("seed", game.GetProperty("team1").GetProperty("kind").GetString());
        Assert.True(game.GetProperty("team1").TryGetProperty("ref", out _));

        // Every deck code appears across the full bracket.
        var codes = root.GetProperty("games").EnumerateArray()
            .Select(g => g.GetProperty("bracket").GetString())
            .Distinct()
            .ToList();
        Assert.Contains("W", codes);
        Assert.Contains("B1", codes);
        Assert.Contains("B2", codes);
        Assert.Contains("F", codes);
    }
}
