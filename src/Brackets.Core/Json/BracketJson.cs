using System.Text.Json;
using System.Text.Json.Serialization;
using Brackets.Core.Models;

namespace Brackets.Core.Json;

/// <summary>Serializes and deserializes a <see cref="Bracket"/> to the public JSON shape.</summary>
public static class BracketJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(Bracket bracket) => JsonSerializer.Serialize(ToDto(bracket), Options);

    public static Bracket Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<BracketDto>(json, Options)
                  ?? throw new JsonException("Bracket JSON deserialized to null.");
        return FromDto(dto);
    }

    private static BracketDto ToDto(Bracket bracket) => new()
    {
        TeamCount = bracket.TeamCount,
        Format = bracket.Format,
        SequenceCount = bracket.SequenceCount,
        Games = bracket.Games.Select(ToDto).ToList(),
        Byes = bracket.Byes
            .Select(b => new ByeDto { Bracket = DeckCode(b.Deck), Round = b.Round, Slot = ToDto(b.Slot) })
            .ToList(),
        TeamNames = bracket.TeamNames.Count == 0 ? null : new Dictionary<int, string>(bracket.TeamNames),
    };

    private static GameDto ToDto(Game g) => new()
    {
        GameNumber = g.GameNumber,
        SequenceNumber = g.SequenceNumber,
        Bracket = DeckCode(g.Deck),
        Round = g.Round,
        Team1 = ToDto(g.Team1),
        Team2 = ToDto(g.Team2),
        WinnerNextGame = g.WinnerNextGame,
        WinnerNextSlot = g.WinnerNextSlot,
        LoserNextGame = g.LoserNextGame,
        LoserNextSlot = g.LoserNextSlot,
        IfNecessary = g.IfNecessary ? true : null,
        Note = g.Note,
    };

    private static SlotRefDto ToDto(SlotRef s) => new() { Kind = KindCode(s.Kind), Ref = s.Ref };

    private static Bracket FromDto(BracketDto dto) => new()
    {
        TeamCount = dto.TeamCount,
        Format = dto.Format ?? "three-life",
        Games = (dto.Games ?? new()).Select(FromDto).ToList(),
        Byes = (dto.Byes ?? new())
            .Select(b => new Bye { Deck = ParseDeck(b.Bracket), Round = b.Round, Slot = FromDto(b.Slot) })
            .ToList(),
        TeamNames = dto.TeamNames ?? new Dictionary<int, string>(),
    };

    private static Game FromDto(GameDto g) => new()
    {
        GameNumber = g.GameNumber,
        SequenceNumber = g.SequenceNumber,
        Deck = ParseDeck(g.Bracket),
        Round = g.Round,
        Team1 = FromDto(g.Team1),
        Team2 = FromDto(g.Team2),
        WinnerNextGame = g.WinnerNextGame,
        WinnerNextSlot = g.WinnerNextSlot,
        LoserNextGame = g.LoserNextGame,
        LoserNextSlot = g.LoserNextSlot,
        IfNecessary = g.IfNecessary ?? false,
        Note = g.Note,
    };

    private static SlotRef FromDto(SlotRefDto? s)
    {
        if (s is null)
        {
            return SlotRef.Seed(0);
        }

        return s.Kind switch
        {
            "seed" => SlotRef.Seed(s.Ref),
            "winnerOf" => SlotRef.WinnerOf(s.Ref),
            "loserOf" => SlotRef.LoserOf(s.Ref),
            _ => throw new JsonException($"Unknown slot kind '{s.Kind}'."),
        };
    }

    internal static string DeckCode(Deck deck) => deck switch
    {
        Deck.Winners => "W",
        Deck.LowerOne => "B1",
        Deck.LowerTwo => "B2",
        Deck.Finals => "F",
        _ => throw new ArgumentOutOfRangeException(nameof(deck)),
    };

    private static Deck ParseDeck(string? code) => code switch
    {
        "W" => Deck.Winners,
        "B1" => Deck.LowerOne,
        "B2" => Deck.LowerTwo,
        "F" => Deck.Finals,
        _ => throw new JsonException($"Unknown bracket code '{code}'."),
    };

    private static string KindCode(SlotKind kind) => kind switch
    {
        SlotKind.Seed => "seed",
        SlotKind.WinnerOf => "winnerOf",
        SlotKind.LoserOf => "loserOf",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    // ---- wire DTOs ----

    private sealed class BracketDto
    {
        public int TeamCount { get; set; }
        public string? Format { get; set; }
        public int SequenceCount { get; set; }
        public List<GameDto>? Games { get; set; }
        public List<ByeDto>? Byes { get; set; }
        public Dictionary<int, string>? TeamNames { get; set; }
    }

    private sealed class GameDto
    {
        public int GameNumber { get; set; }
        public int SequenceNumber { get; set; }
        public string Bracket { get; set; } = "W";
        public int Round { get; set; }
        public SlotRefDto Team1 { get; set; } = new();
        public SlotRefDto Team2 { get; set; } = new();
        public int? WinnerNextGame { get; set; }
        public int? WinnerNextSlot { get; set; }
        public int? LoserNextGame { get; set; }
        public int? LoserNextSlot { get; set; }
        public bool? IfNecessary { get; set; }
        public string? Note { get; set; }
    }

    private sealed class SlotRefDto
    {
        public string Kind { get; set; } = "seed";
        public int Ref { get; set; }
    }

    private sealed class ByeDto
    {
        public string Bracket { get; set; } = "W";
        public int Round { get; set; }
        public SlotRefDto Slot { get; set; } = new();
    }
}
