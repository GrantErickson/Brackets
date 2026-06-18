using Brackets.Core.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brackets.Pdf;

/// <summary>
/// Renders a <see cref="Bracket"/> to a blank, hand-fillable PDF "game sheet" on US Letter paper in
/// landscape. Games are grouped by synchronous time slot (sequence number) so an organizer can write a
/// start time per round, fill in team names as they are seeded, and record scores and winners by hand.
/// </summary>
public static class PdfBracketRenderer
{
    static PdfBracketRenderer()
    {
        // QuestPDF Community license: free for individuals, FOSS, and organizations under $1M revenue.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string Ink = Colors.Black;
    private static readonly string Line = Colors.Grey.Medium;
    private static readonly string DeckTint = Colors.Grey.Lighten3;

    public static byte[] Render(Bracket bracket) => BuildDocument(bracket).GeneratePdf();

    public static void Save(Bracket bracket, string path) => BuildDocument(bracket).GeneratePdf(path);

    /// <summary>Rasterizes each page to a PNG (useful for previews/thumbnails).</summary>
    public static IReadOnlyList<byte[]> RenderImages(Bracket bracket, int dpi = 110) =>
        BuildDocument(bracket).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();

    private static IDocument BuildDocument(Bracket bracket)
    {
        var bySequence = bracket.Games
            .GroupBy(g => g.SequenceNumber)
            .OrderBy(grp => grp.Key)
            .ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink));

                page.Header().Element(h => Header(h, bracket));
                page.Content().PaddingTop(6).Column(col =>
                {
                    col.Spacing(8);
                    foreach (var round in bySequence)
                    {
                        col.Item().Element(e => SequenceBlock(e, round.Key, round.OrderBy(g => g.GameNumber).ToList(), bracket));
                    }
                });

                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("Three-life bracket: a team is eliminated only on its third loss. W = winners (0 losses), B1 = 1 loss, B2 = 2 losses, F = finals.")
                        .FontSize(7).FontColor(Line);
                    row.ConstantItem(120).AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontSize(7).FontColor(Line);
                        t.CurrentPageNumber().FontSize(7).FontColor(Line);
                        t.Span(" / ").FontSize(7).FontColor(Line);
                        t.TotalPages().FontSize(7).FontColor(Line);
                    });
                });
            });
        });
    }

    private static void Header(IContainer container, Bracket bracket)
    {
        container.BorderBottom(1).BorderColor(Line).PaddingBottom(6).Row(row =>
        {
            row.RelativeItem().Column(col =>
            {
                col.Item().Text("Three-Life Elimination Bracket").FontSize(16).Bold();
                col.Item().Text($"{bracket.TeamCount} teams  ·  {bracket.Games.Count} games  ·  {bracket.SequenceCount} synchronous rounds  ·  every team plays at least 3 games")
                    .FontSize(8).FontColor(Line);
            });

            row.ConstantItem(220).Column(col =>
            {
                col.Item().AlignRight().Text("Tournament: ____________________").FontSize(9);
                col.Item().PaddingTop(4).AlignRight().Text("Date: __________  Champion: __________").FontSize(9);
            });
        });
    }

    private static void SequenceBlock(IContainer container, int sequence, List<Game> games, Bracket bracket)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.AutoItem().Background(Ink).PaddingVertical(2).PaddingHorizontal(8)
                    .Text($"ROUND {sequence}").FontColor(Colors.White).Bold().FontSize(10);
                row.RelativeItem().PaddingLeft(10).AlignMiddle().Text("Start time: ______________      (all games in this round are played at the same time)")
                    .FontSize(8).FontColor(Line);
            });

            col.Item().PaddingTop(4).Inlined(inlined =>
            {
                inlined.Spacing(8);
                inlined.VerticalSpacing(8);
                foreach (var game in games)
                {
                    inlined.Item().Width(232).Element(e => GameCard(e, game, bracket));
                }
            });
        });
    }

    private static void GameCard(IContainer container, Game game, Bracket bracket)
    {
        container.Border(1).BorderColor(Line).Background(Colors.White).Column(col =>
        {
            col.Item().Background(DeckTint).PaddingVertical(3).PaddingHorizontal(6).Row(row =>
            {
                row.RelativeItem().Text(t =>
                {
                    t.Span($"Game {game.GameNumber}").Bold().FontSize(10);
                    t.Span($"   {DeckLabel(game.Deck)}").FontSize(8).FontColor(Line);
                    if (game.IfNecessary)
                    {
                        t.Span("   (if necessary)").FontSize(8).FontColor(Colors.Red.Medium);
                    }
                });
            });

            col.Item().PaddingHorizontal(6).PaddingTop(4).Element(e => TeamRow(e, game.Team1, bracket));
            col.Item().PaddingHorizontal(6).PaddingTop(3).Text("vs").FontSize(7).FontColor(Line);
            col.Item().PaddingHorizontal(6).Element(e => TeamRow(e, game.Team2, bracket));

            col.Item().PaddingHorizontal(6).PaddingTop(4).PaddingBottom(5).Text(t =>
            {
                t.Span("Winner → ").FontSize(7).FontColor(Line);
                t.Span(NextLabel(game.WinnerNextGame, "CHAMPION")).FontSize(7).Bold();
                t.Span("     Loser → ").FontSize(7).FontColor(Line);
                t.Span(NextLabel(game.LoserNextGame, "eliminated")).FontSize(7).Bold();
            });
        });
    }

    private static void TeamRow(IContainer container, SlotRef slot, Bracket bracket)
    {
        container.Row(row =>
        {
            row.ConstantItem(54).AlignBottom().Text(SourceLabel(slot, bracket)).FontSize(7).FontColor(Line);
            row.RelativeItem().PaddingHorizontal(4).BorderBottom(1).BorderColor(Line).Height(15);
            row.ConstantItem(26).Height(15).Border(1).BorderColor(Line); // score box
        });
    }

    private static string DeckLabel(Deck deck) => deck switch
    {
        Deck.Winners => "Winners (0 losses)",
        Deck.LowerOne => "1-loss deck",
        Deck.LowerTwo => "2-loss deck",
        Deck.Finals => "Finals",
        _ => string.Empty,
    };

    private static string SourceLabel(SlotRef slot, Bracket bracket) => slot.Kind switch
    {
        SlotKind.Seed => bracket.TeamNames.TryGetValue(slot.Ref, out var name) ? name : $"Seed {slot.Ref}",
        SlotKind.WinnerOf => $"Win #{slot.Ref}",
        SlotKind.LoserOf => $"Lose #{slot.Ref}",
        _ => string.Empty,
    };

    private static string NextLabel(int? gameNumber, string terminal) =>
        gameNumber is int g ? $"Game {g}" : terminal;
}
