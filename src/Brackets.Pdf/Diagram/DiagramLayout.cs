using Brackets.Core.Models;

namespace Brackets.Pdf.Diagram;

/// <summary>
/// Computes the geometry of a traditional left-to-right bracket diagram for a three-life bracket.
/// Pure: produces a <see cref="DiagramScene"/> (positioned boxes, leaf tags, connector lines and labels)
/// with no rendering dependency, so the layout can be unit-tested directly.
///
/// Each deck's winner edges form a proper binary tree. We lay each deck out left-to-right with its
/// entry leaves at column 0 (so the Winners seeds sit at the far left), stack the three decks in
/// vertical bands (Winners, then 1-loss, then 2-loss), and place the finals to the right. Columns come
/// solely from the layout recursion (child = parent - 1); <see cref="Game.Round"/> is intentionally not
/// used for x, because a same-deck winner edge can span several rounds.
/// </summary>
public static class DiagramLayout
{
    public const float BoxW = 154;
    public const float BoxH = 46;
    public const float ColGap = 74;
    public const float ColPitch = BoxW + ColGap;     // 228
    public const float RowH = 60;                    // vertical pitch of one leaf track (> BoxH => no overlap)
    public const float LeafW = 120;
    public const float LeafH = 30;
    public const float BandGap = 40;
    public const float Margin = 36;
    public const float HeaderH = 80;
    public const float FooterH = 26;
    public const float Lw = 1.5f;
    public const float Gutter = ColGap / 2;          // elbow bus, left of a parent box

    internal const string Ink = "#111111";
    internal const string LineColor = "#7C8893";
    internal const string BandColor = "#F3F5F7";
    internal const string Red = "#B03A2E";
    internal const string Grey = "#8A8A8A";

    private static readonly Deck[] LowerDecks = { Deck.Winners, Deck.LowerOne, Deck.LowerTwo };

    public static DiagramScene Build(Bracket bracket) => new Builder(bracket).Run();

    private sealed class Builder
    {
        private readonly Bracket _bracket;
        private readonly Dictionary<int, Game> _byNumber;
        private readonly List<DiagramItem> _bands = new();
        private readonly List<DiagramItem> _lines = new();
        private readonly List<DiagramItem> _leaves = new();
        private readonly List<DiagramItem> _boxes = new();
        private readonly List<DiagramItem> _labels = new();

        private readonly Dictionary<int, int> _col = new();        // game number -> column
        private readonly Dictionary<int, float> _centerY = new();  // game number -> center y
        private readonly Dictionary<(int Game, int Slot), LeafPos> _leafPos = new();

        private float _leafCursor;

        public Builder(Bracket bracket)
        {
            _bracket = bracket;
            _byNumber = bracket.Games.ToDictionary(g => g.GameNumber);
        }

        private readonly record struct LeafPos(int Column, float CenterY, SlotRef Slot);

        public DiagramScene Run()
        {
            var champion = LowerDecks.ToDictionary(d => d, ChampionOf);
            var height = LowerDecks.ToDictionary(d => d, d => Height(champion[d]));
            int maxChampCol = LowerDecks.Max(d => height[d]);

            // Lay out each deck as a band; leaves land at column 0 (left edge).
            float top = Margin + HeaderH;
            var bandTop = new Dictionary<Deck, float>();
            foreach (var deck in LowerDecks)
            {
                bandTop[deck] = top;
                _leafCursor = 0;
                Layout(champion[deck], height[deck], top, deck);
                float bandHeight = _leafCursor * RowH;
                AddBand(deck, top, bandHeight);
                top += bandHeight + BandGap;
            }

            float contentBottom = top - BandGap;
            PlaceFinals(champion, maxChampCol, Margin + HeaderH, contentBottom);

            // Emit connectors, leaves, boxes and annotations.
            foreach (var game in _bracket.Games)
            {
                EmitElbow(game);
            }

            foreach (var ((gameNumber, _), leaf) in _leafPos)
            {
                EmitLeaf(_byNumber[gameNumber], leaf);
            }

            foreach (var game in _bracket.Games)
            {
                EmitGame(game);
            }

            int rightmostCol = _col.Values.Max();
            float width = X(rightmostCol) + BoxW + Margin;
            float height2 = contentBottom + FooterH + Margin;

            EmitHeaderAndLegend(width, height2);

            var items = new List<DiagramItem>();
            items.AddRange(_bands);
            items.AddRange(_lines);
            items.AddRange(_leaves);
            items.AddRange(_boxes);
            items.AddRange(_labels);
            return new DiagramScene(items, width, height2, _bracket.TeamCount);
        }

        // ---- structure helpers ----

        private Game? SameDeckChild(SlotRef slot, Deck deck) =>
            slot.Kind == SlotKind.WinnerOf && _byNumber.TryGetValue(slot.Ref, out var g) && g.Deck == deck ? g : null;

        private Game ChampionOf(Deck deck) => _bracket.Games.Single(g =>
            g.Deck == deck && (g.WinnerNextGame is null || _byNumber[g.WinnerNextGame.Value].Deck != deck));

        private int Height(Game g)
        {
            int h = 0;
            foreach (var slot in new[] { g.Team1, g.Team2 })
            {
                if (SameDeckChild(slot, g.Deck) is Game child)
                {
                    h = Math.Max(h, Height(child));
                }
            }

            return h + 1;
        }

        // ---- layout recursion: assigns columns (child = parent - 1) and y from leaf tracks ----

        private float Layout(Game g, int col, float bandTop, Deck deck)
        {
            _col[g.GameNumber] = col;
            var slots = new[] { g.Team1, g.Team2 };
            var centers = new float[2];
            for (int i = 0; i < 2; i++)
            {
                if (SameDeckChild(slots[i], deck) is Game child)
                {
                    centers[i] = Layout(child, col - 1, bandTop, deck);
                }
                else
                {
                    float cy = bandTop + (_leafCursor * RowH) + (RowH / 2);
                    _leafCursor++;
                    _leafPos[(g.GameNumber, i)] = new LeafPos(col - 1, cy, slots[i]);
                    centers[i] = cy;
                }
            }

            float center = (centers[0] + centers[1]) / 2;
            _centerY[g.GameNumber] = center;
            return center;
        }

        private void PlaceFinals(IReadOnlyDictionary<Deck, Game> champion, int maxChampCol, float top, float bottom)
        {
            var finals = _bracket.Games.Where(g => g.Deck == Deck.Finals).ToList();
            var reset = finals.Single(g => g.WinnerNextGame is null);
            var grand = finals.Single(g => g.WinnerNextGame == reset.GameNumber);
            var semi = finals.Single(g => g.WinnerNextGame == grand.GameNumber);

            float Limit(float y) => Math.Clamp(y, top + (BoxH / 2), bottom - (BoxH / 2));

            float semiY = (_centerY[champion[Deck.LowerOne].GameNumber] + _centerY[champion[Deck.LowerTwo].GameNumber]) / 2;
            float grandY = (_centerY[champion[Deck.Winners].GameNumber] + semiY) / 2;
            float resetY = grandY + (RowH * 0.95f);

            _col[semi.GameNumber] = maxChampCol + 1;
            _centerY[semi.GameNumber] = Limit(semiY);
            _col[grand.GameNumber] = maxChampCol + 2;
            _centerY[grand.GameNumber] = Limit(grandY);
            _col[reset.GameNumber] = maxChampCol + 3;
            _centerY[reset.GameNumber] = Limit(resetY);
        }

        // ---- emit ----

        private static float X(int col) => Margin + (col * ColPitch);

        private Rect GameRect(Game g) => new(X(_col[g.GameNumber]), _centerY[g.GameNumber] - (BoxH / 2), BoxW, BoxH);

        private Rect LeafRect(LeafPos leaf) => new(X(leaf.Column), leaf.CenterY - (LeafH / 2), LeafW, LeafH);

        private void AddBand(Deck deck, float top, float height)
        {
            float pad = (RowH - BoxH) / 2;
            _bands.Add(new BandItem
            {
                R = new Rect(Margin - 6, top - pad, 100000, height), // width clamped later by page; tint only
                Color = BandColor,
            });
            _labels.Add(new LabelItem
            {
                R = new Rect(Margin, top - pad + 2, 220, 12),
                Text = DeckBandLabel(deck),
                Size = 9,
                Bold = true,
                Color = Grey,
                Align = DiagramAlign.Left,
            });
        }

        private void EmitElbow(Game g)
        {
            var sources = new List<(float Right, float Cy)>();
            var slots = new[] { g.Team1, g.Team2 };
            for (int i = 0; i < 2; i++)
            {
                if (SameDeckChild(slots[i], g.Deck) is Game child)
                {
                    sources.Add((GameRect(child).Right, _centerY[child.GameNumber]));
                }
                else if (_leafPos.TryGetValue((g.GameNumber, i), out var leaf))
                {
                    sources.Add((LeafRect(leaf).Right, leaf.CenterY));
                }
                // else: cross-deck input (finals feed) -> no line, shown via labels.
            }

            if (sources.Count == 0)
            {
                return;
            }

            var rect = GameRect(g);
            float parentLeft = rect.X;
            float parentCy = rect.CenterY;
            float busX = parentLeft - Gutter;

            float top = parentCy;
            float low = parentCy;
            foreach (var (right, cy) in sources)
            {
                HLine(right, cy, busX - right);
                top = Math.Min(top, cy);
                low = Math.Max(low, cy);
            }

            VLine(busX, top, low - top);
            HLine(busX, parentCy, parentLeft - busX);
        }

        private void HLine(float x, float cy, float length)
        {
            if (length <= 0)
            {
                return;
            }

            _lines.Add(new LineItem { R = new Rect(x, cy - (Lw / 2), length, Lw), Color = LineColor });
        }

        private void VLine(float x, float y, float length)
        {
            if (length <= 0)
            {
                return;
            }

            _lines.Add(new LineItem { R = new Rect(x - (Lw / 2), y, Lw, length), Color = LineColor });
        }

        private void EmitLeaf(Game owner, LeafPos leaf)
        {
            bool isBye = leaf.Slot.Kind == SlotKind.Seed
                         && _bracket.Byes.Any(b => b.Slot.Kind == SlotKind.Seed && b.Slot.Ref == leaf.Slot.Ref);
            _leaves.Add(new LeafItem { R = LeafRect(leaf), Column = leaf.Column, Slot = leaf.Slot, IsBye = isBye });
        }

        private void EmitGame(Game g)
        {
            bool isChampion = g.WinnerNextGame is null || (_byNumber.TryGetValue(g.WinnerNextGame.Value, out var nx) && nx.Deck != g.Deck && g.Deck != Deck.Finals);
            var rect = GameRect(g);
            _boxes.Add(new GameItem { R = rect, Column = _col[g.GameNumber], Game = g, IsChampion = isChampion });

            // Loser-drop annotation at the box's bottom-right (cross-deck "teleport" label).
            if (g.LoserNextGame is int loserGame)
            {
                _labels.Add(new LabelItem
                {
                    R = new Rect(rect.X, rect.Bottom + 1, BoxW, 9),
                    Text = $"L → G{loserGame}",
                    Size = 7,
                    Color = Red,
                    Align = DiagramAlign.Right,
                });
            }
            else if (g.Deck != Deck.Finals)
            {
                _labels.Add(new LabelItem
                {
                    R = new Rect(rect.X, rect.Bottom + 1, BoxW, 9),
                    Text = "loser out",
                    Size = 7,
                    Color = Grey,
                    Align = DiagramAlign.Right,
                });
            }

            // Winner-to-finals annotation for deck champions.
            if (isChampion && g.WinnerNextGame is int winnerGame)
            {
                _labels.Add(new LabelItem
                {
                    R = new Rect(rect.X, rect.Y - 10, BoxW, 9),
                    Text = $"winner → G{winnerGame}",
                    Size = 7,
                    Bold = true,
                    Color = Ink,
                    Align = DiagramAlign.Right,
                });
            }
        }

        private void EmitHeaderAndLegend(float width, float height)
        {
            _labels.Add(new LabelItem
            {
                R = new Rect(Margin, Margin + 2, width - (2 * Margin), 40),
                Text = "Three-Life Elimination Bracket",
                Size = 16,
                Bold = true,
                Color = Ink,
                Align = DiagramAlign.Left,
            });
            _labels.Add(new LabelItem
            {
                R = new Rect(Margin, Margin + 46, width - (2 * Margin), 18),
                Text = $"{_bracket.TeamCount} teams  ·  {_bracket.Games.Count} games  ·  out on the 3rd loss  ·  every team plays ≥ 3 games  ·  follow L → G# down to the next deck",
                Size = 9,
                Color = Grey,
                Align = DiagramAlign.Left,
            });
            _labels.Add(new LabelItem
            {
                R = new Rect(Margin, height - Margin - 12, width - (2 * Margin), 10),
                Text = "Teams start at the left and advance right; the loser of each game drops to the deck below (matching L → G# / L# numbers). Champion is at the far right.",
                Size = 7.5f,
                Color = Grey,
                Align = DiagramAlign.Left,
            });
        }

        private static string DeckBandLabel(Deck deck) => deck switch
        {
            Deck.Winners => "WINNERS  ·  0 losses",
            Deck.LowerOne => "1-LOSS DECK",
            Deck.LowerTwo => "2-LOSS DECK  ·  lose again = out",
            _ => string.Empty,
        };
    }
}
