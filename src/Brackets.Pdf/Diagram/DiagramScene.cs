using Brackets.Core.Models;

namespace Brackets.Pdf.Diagram;

/// <summary>Axis-aligned rectangle in PDF points (origin top-left).</summary>
public readonly record struct Rect(float X, float Y, float W, float H)
{
    public float Right => X + W;
    public float Bottom => Y + H;
    public float CenterY => Y + (H / 2);

    public bool Intersects(Rect o) =>
        X < o.Right - 0.05f && Right > o.X + 0.05f && Y < o.Bottom - 0.05f && Bottom > o.Y + 0.05f;
}

public enum DiagramAlign
{
    Left,
    Center,
    Right,
}

/// <summary>A positioned drawing primitive in the computed diagram (pure geometry, no rendering dependency).</summary>
public abstract record DiagramItem
{
    public Rect R { get; init; }
}

/// <summary>A thin filled rectangle used as an axis-aligned connector line.</summary>
public sealed record LineItem : DiagramItem
{
    public string Color { get; init; } = "#000000";
}

/// <summary>A band background tint for a deck.</summary>
public sealed record BandItem : DiagramItem
{
    public string Color { get; init; } = "#F4F4F4";
}

/// <summary>A game box. <see cref="Column"/> is the assigned bracket column (leftmost = 0).</summary>
public sealed record GameItem : DiagramItem
{
    public int Column { get; init; }
    public Game Game { get; init; } = null!;
    public bool IsChampion { get; init; }
}

/// <summary>An entry tag (a seed, a bye seed, or a cross-deck drop-in) feeding a game from the left.</summary>
public sealed record LeafItem : DiagramItem
{
    public int Column { get; init; }
    public SlotRef Slot { get; init; } = SlotRef.Seed(0);
    public bool IsBye { get; init; }
}

/// <summary>Free-floating text (titles, deck labels, loser-drop and finals annotations).</summary>
public sealed record LabelItem : DiagramItem
{
    public string Text { get; init; } = string.Empty;
    public float Size { get; init; } = 8;
    public bool Bold { get; init; }
    public string Color { get; init; } = "#111111";
    public DiagramAlign Align { get; init; } = DiagramAlign.Left;
}

/// <summary>The fully positioned diagram: all items plus the overall canvas size in points.</summary>
public sealed record DiagramScene(IReadOnlyList<DiagramItem> Items, float Width, float Height, int TeamCount);
