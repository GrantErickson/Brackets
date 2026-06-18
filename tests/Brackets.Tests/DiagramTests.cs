using Brackets.Core.Generation;
using Brackets.Core.Models;
using Brackets.Pdf;
using Brackets.Pdf.Diagram;
using Xunit;

namespace Brackets.Tests;

public class DiagramTests
{
    public static IEnumerable<object[]> TeamCounts()
    {
        for (int n = BracketOptions.MinTeams; n <= BracketOptions.MaxTeams; n++)
        {
            yield return new object[] { n };
        }
    }

    private static DiagramScene Scene(int n) =>
        BracketDiagramRenderer.BuildScene(BracketGenerator.Generate(new BracketOptions { TeamCount = n }));

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void No_two_game_or_leaf_boxes_overlap(int n)
    {
        var boxes = Scene(n).Items
            .Where(i => i is GameItem or LeafItem)
            .Select(i => i.R)
            .ToList();

        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                Assert.False(boxes[i].Intersects(boxes[j]), $"Boxes overlap: {boxes[i]} and {boxes[j]} (n={n}).");
            }
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Same_deck_winner_child_is_exactly_one_column_left_of_its_parent(int n)
    {
        var scene = Scene(n);
        var games = scene.Items.OfType<GameItem>().ToList();
        var columnOf = games.ToDictionary(g => g.Game.GameNumber, g => g.Column);
        var deckOf = games.ToDictionary(g => g.Game.GameNumber, g => g.Game.Deck);

        foreach (var item in games)
        {
            foreach (var slot in new[] { item.Game.Team1, item.Game.Team2 })
            {
                if (slot.Kind == SlotKind.WinnerOf
                    && deckOf.TryGetValue(slot.Ref, out var childDeck)
                    && childDeck == item.Game.Deck)
                {
                    Assert.Equal(columnOf[item.Game.GameNumber] - 1, columnOf[slot.Ref]);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Teams_start_at_the_left_edge(int n)
    {
        var scene = Scene(n);
        // The leftmost content overall is an entry leaf (a seed / drop-in), and it sits at the left margin.
        float leftmostLeaf = scene.Items.OfType<LeafItem>().Min(l => l.R.X);
        float leftmostGame = scene.Items.OfType<GameItem>().Min(g => g.R.X);
        Assert.True(leftmostLeaf < leftmostGame, "Entry tags should start left of the first game column.");
        Assert.Equal(DiagramLayout.Margin, leftmostLeaf, 1f);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Champion_game_is_the_rightmost_box(int n)
    {
        var scene = Scene(n);
        var rightmost = scene.Items.OfType<GameItem>().OrderByDescending(g => g.R.X).First();
        Assert.True(rightmost.Game.WinnerNextGame is null, "Rightmost box should be the championship (reset) game.");
        Assert.True(rightmost.Game.IfNecessary);
    }

    [Theory]
    [MemberData(nameof(TeamCounts))]
    public void Renders_a_non_empty_pdf(int n)
    {
        byte[] pdf = BracketDiagramRenderer.Render(BracketGenerator.Generate(new BracketOptions { TeamCount = n }));
        Assert.True(pdf.Length > 1000, $"Diagram PDF for {n} teams was only {pdf.Length} bytes.");
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("%PDF"), pdf.Take(4).ToArray());
    }

    [Fact]
    public void Renders_with_long_team_names_without_overflowing()
    {
        var names = Enumerable.Range(1, 14)
            .ToDictionary(i => i, i => $"The Exceptionally Long Team Name Number {i}");
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 14, TeamNames = names });
        byte[] pdf = BracketDiagramRenderer.Render(bracket); // must not throw a layout/overflow exception
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Scene_has_positive_bounds_and_every_game_is_boxed()
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 12 });
        var scene = BracketDiagramRenderer.BuildScene(bracket);
        Assert.True(scene.Width > 0 && scene.Height > 0);
        Assert.Equal(bracket.Games.Count, scene.Items.OfType<GameItem>().Count());
    }
}
