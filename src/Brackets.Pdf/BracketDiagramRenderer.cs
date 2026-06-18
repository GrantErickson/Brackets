using Brackets.Core.Models;
using Brackets.Pdf.Diagram;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Brackets.Pdf;

/// <summary>
/// Renders a <see cref="Bracket"/> as a traditional left-to-right bracket DIAGRAM: teams start at the
/// left and advance to the right, with classic connector "elbow" lines. The three decks are stacked in
/// vertical bands (Winners, 1-loss, 2-loss) and the finals sit at the right. Cross-deck loser drops are
/// shown as matched "L → G#" / "L#" labels rather than long crossing lines. The page is sized to fit the
/// whole diagram (poster style); use the companion <see cref="PdfBracketRenderer"/> for a printable
/// per-round game sheet instead.
/// </summary>
public static class BracketDiagramRenderer
{
    static BracketDiagramRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const float MaxPagePoints = 14000f; // PDF spec hard limit is 14400pt.
    private const string LineGrey = "#5A6670";

    public static DiagramScene BuildScene(Bracket bracket) => DiagramLayout.Build(bracket);

    public static byte[] Render(Bracket bracket) => BuildDocument(bracket).GeneratePdf();

    public static void Save(Bracket bracket, string path) => BuildDocument(bracket).GeneratePdf(path);

    /// <summary>Rasterizes each page to a PNG (useful for previews/thumbnails).</summary>
    public static IReadOnlyList<byte[]> RenderImages(Bracket bracket, int dpi = 150) =>
        BuildDocument(bracket).GenerateImages(new ImageGenerationSettings { RasterDpi = dpi }).ToList();

    private static IDocument BuildDocument(Bracket bracket)
    {
        var scene = DiagramLayout.Build(bracket);
        float scale = Math.Min(1f, Math.Min(MaxPagePoints / scene.Width, MaxPagePoints / scene.Height));

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(new PageSize(scene.Width * scale, scene.Height * scale));
                page.Margin(0);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(DiagramLayout.Ink));
                page.Content().Scale(scale).Element(c => EmitScene(c, scene, bracket));
            });
        });
    }

    private static void EmitScene(IContainer root, DiagramScene scene, Bracket bracket) => root.Layers(layers =>
    {
        layers.PrimaryLayer().Width(scene.Width).Height(scene.Height);
        foreach (var item in scene.Items)
        {
            layers.Layer().Element(c => Place(c, item, scene, bracket));
        }
    });

    private static IContainer At(IContainer c, Rect r) =>
        c.PaddingLeft(r.X).PaddingTop(r.Y).Width(r.W).Height(r.H);

    private static void Place(IContainer c, DiagramItem item, DiagramScene scene, Bracket bracket)
    {
        switch (item)
        {
            case BandItem band:
                float w = Math.Min(band.R.W, scene.Width - band.R.X - 2);
                c.PaddingLeft(band.R.X).PaddingTop(band.R.Y).Width(w).Height(band.R.H).Background(band.Color);
                break;
            case LineItem line:
                At(c, line.R).Background(line.Color);
                break;
            case LabelItem label:
                var text = Align(At(c, label.R), label.Align)
                    .Text(label.Text).FontSize(label.Size).FontColor(label.Color);
                if (label.Bold)
                {
                    text.Bold();
                }

                break;
            case LeafItem leaf:
                At(c, leaf.R).Element(x => LeafTag(x, leaf, bracket));
                break;
            case GameItem game:
                At(c, game.R).Element(x => GameBox(x, game.Game, bracket));
                break;
        }
    }

    private static IContainer Align(IContainer c, DiagramAlign align) => align switch
    {
        DiagramAlign.Right => c.AlignRight(),
        DiagramAlign.Center => c.AlignCenter(),
        _ => c.AlignLeft(),
    };

    private static void GameBox(IContainer c, Game g, Bracket bracket)
    {
        string border = g.IfNecessary ? DiagramLayout.Red : LineGrey;
        c.Border(1).BorderColor(border).Background(Colors.White).Column(col =>
        {
            col.Item()
                .Background(g.IfNecessary ? "#F6E0DC" : "#EBEFF2")
                .PaddingHorizontal(4).PaddingVertical(2)
                .Row(row =>
                {
                    row.RelativeItem().Text($"G{g.GameNumber}").Bold().FontSize(8).FontColor(DiagramLayout.Ink);
                    row.AutoItem().AlignRight().Text(g.IfNecessary ? "RESET · if nec." : DeckCode(g.Deck))
                        .FontSize(6.5f).FontColor(g.IfNecessary ? DiagramLayout.Red : DiagramLayout.Grey);
                });

            col.Item().Element(e => TeamRow(e, g.Team1, bracket));
            col.Item().Element(e => TeamRow(e, g.Team2, bracket));
        });
    }

    private static void TeamRow(IContainer c, SlotRef slot, Bracket bracket)
    {
        c.PaddingHorizontal(4).PaddingTop(2).Row(row =>
        {
            row.ConstantItem(40).AlignMiddle().Text(SourceLabel(slot, bracket))
                .FontSize(6.5f).FontColor(DiagramLayout.Grey);
            row.RelativeItem().PaddingHorizontal(3).AlignBottom()
                .BorderBottom(0.75f).BorderColor(DiagramLayout.LineColor)
                .Text(t =>
                {
                    t.Span(NameHint(slot, bracket)).FontSize(7);
                    t.ClampLines(1);
                });
            row.ConstantItem(20).Height(11).Border(0.75f).BorderColor(DiagramLayout.LineColor);
        });
    }

    private static void LeafTag(IContainer c, LeafItem leaf, Bracket bracket)
    {
        c.Border(0.75f).BorderColor(DiagramLayout.LineColor).Background(Colors.White)
            .PaddingHorizontal(3).PaddingVertical(2).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Text(SourceLabel(leaf.Slot, bracket)).Bold().FontSize(7).FontColor(DiagramLayout.Ink);
                    if (leaf.IsBye)
                    {
                        row.AutoItem().Text("BYE").FontSize(5.5f).FontColor(DiagramLayout.Red);
                    }
                });

                col.Item().PaddingTop(1).Row(row =>
                {
                    row.RelativeItem().AlignBottom().BorderBottom(0.75f).BorderColor(DiagramLayout.LineColor)
                        .Text(t =>
                        {
                            t.Span(NameHint(leaf.Slot, bracket)).FontSize(7);
                            t.ClampLines(1);
                        });
                    row.ConstantItem(18).Height(9).Border(0.75f).BorderColor(DiagramLayout.LineColor);
                });
            });
    }

    private static string SourceLabel(SlotRef slot, Bracket bracket) => slot.Kind switch
    {
        SlotKind.Seed => $"Seed {slot.Ref}",
        SlotKind.WinnerOf => $"W{slot.Ref}",
        SlotKind.LoserOf => $"L{slot.Ref}",
        _ => string.Empty,
    };

    private static string NameHint(SlotRef slot, Bracket bracket) =>
        slot.Kind == SlotKind.Seed && bracket.TeamNames.TryGetValue(slot.Ref, out var name) ? name : string.Empty;

    private static string DeckCode(Deck deck) => deck switch
    {
        Deck.Winners => "W",
        Deck.LowerOne => "B1",
        Deck.LowerTwo => "B2",
        Deck.Finals => "F",
        _ => string.Empty,
    };
}
