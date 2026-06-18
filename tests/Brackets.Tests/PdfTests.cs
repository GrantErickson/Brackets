using Brackets.Core.Generation;
using Brackets.Core.Models;
using Brackets.Pdf;
using Xunit;

namespace Brackets.Tests;

public class PdfTests
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
    public void Renders_a_non_empty_pdf(int n)
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = n });
        byte[] pdf = PdfBracketRenderer.Render(bracket);

        Assert.True(pdf.Length > 1000, $"PDF for {n} teams was only {pdf.Length} bytes.");
        // PDF files start with the "%PDF" magic header.
        Assert.Equal(System.Text.Encoding.ASCII.GetBytes("%PDF"), pdf.Take(4).ToArray());
    }

    [Fact]
    public void Renders_with_team_names()
    {
        var names = Enumerable.Range(1, 11).ToDictionary(i => i, i => $"Team {(char)('A' + i - 1)}");
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 11, TeamNames = names });
        byte[] pdf = PdfBracketRenderer.Render(bracket);
        Assert.True(pdf.Length > 1000);
    }

    [Fact]
    public void Renders_page_images()
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 12 });
        var images = PdfBracketRenderer.RenderImages(bracket, dpi: 96);
        Assert.NotEmpty(images);
        Assert.All(images, png => Assert.True(png.Length > 0));
    }
}
