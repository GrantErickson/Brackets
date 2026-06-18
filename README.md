# Brackets

A C# 10 library, CLI, and PDF generator for **three-life elimination** tournament brackets
(8–16 teams). A team is eliminated only on its **third loss**, which guarantees every team plays
**at least three games** and lets any team — even one that loses twice early — climb all the way
back to win the championship. There is no separate consolation bracket: every team always has a
live path to the title.

All games in a round share a **sequence number** and are intended to be played at the same time
(synchronously), so a real start time can be assigned per round.

## How the format works

Teams cascade through three decks by loss count:

| Deck | Losses | A loss here means… |
|------|--------|--------------------|
| **W** (Winners) | 0 | drop to the 1-loss deck |
| **B1** (1-loss) | 1 | drop to the 2-loss deck |
| **B2** (2-loss) | 2 | **eliminated** (third loss) |
| **F** (Finals) | — | the three deck champions converge into one champion |

The minimum path for any team is *lose, lose, lose* = three games. Because three losses are
required to be out and every loss is a game, the three-game guarantee holds automatically — even
for a team that takes its single allowed bye.

**Finals.** The 1-loss and 2-loss champions meet in a semifinal; its winner challenges the
undefeated winners-deck champion in the grand final. If the challenger wins the grand final, a
single **"if-necessary" reset** game is played to decide the title (so the undefeated team must be
beaten twice).

**Byes.** Only the winners-bracket first round can contain byes (when the team count is not a power
of two), and each is given to a distinct top seed — so no team ever gets more than one bye. The
lower decks are built as proper binary trees and contain no byes.

For `N` teams the bracket always has `3N − 3` games (W: `N−1`, B1: `N−2`, B2: `N−3`, finals: 3).

## Projects

| Project | Description |
|---------|-------------|
| `src/Brackets.Core` | Models, the bracket generator, validator, simulator, and JSON. No third-party dependencies. |
| `src/Brackets.Pdf`  | QuestPDF renderer producing a fillable Letter/landscape sheet. |
| `src/Brackets.Cli`  | Command-line interface. |
| `tests/Brackets.Tests` | xUnit invariant, simulation, JSON, and PDF tests (every N in 8..16). |

Targets `net10.0` and compiles as **C# 10** (`<LangVersion>10</LangVersion>`).

## CLI

```
brackets generate --teams <8-16> [--json <path>] [--pdf <path>] [--names <file>]
brackets pdf      (--teams <8-16> | --input <bracket.json>) --output <path>
brackets validate (--teams <8-16> | --input <bracket.json>)
brackets help
```

Examples:

```bash
# Generate JSON and a printable PDF for 12 teams
dotnet run --project src/Brackets.Cli -- generate --teams 12 --json bracket.json --pdf bracket.pdf

# Print the JSON to stdout
dotnet run --project src/Brackets.Cli -- generate --teams 16

# Make a PDF from a previously generated JSON
dotnet run --project src/Brackets.Cli -- pdf --input bracket.json --output sheet.pdf
```

`--names <file>` takes one team name per line and maps them to seeds `1..N` for the PDF; the JSON
identity is always the numeric seed.

## JSON shape

```jsonc
{
  "teamCount": 12,
  "format": "three-life",
  "sequenceCount": 11,
  "games": [
    {
      "gameNumber": 1,          // unique id
      "sequenceNumber": 1,      // synchronous round / time slot
      "bracket": "W",           // W | B1 | B2 | F
      "round": 1,               // round within the deck
      "team1": { "kind": "seed", "ref": 8 },        // seed 8
      "team2": { "kind": "seed", "ref": 9 },        // seed 9
      "winnerNextGame": 5,      // null => this game crowns the champion
      "winnerNextSlot": 2,
      "loserNextGame": 12,      // null => the loser is eliminated
      "loserNextSlot": 1
    }
    // ...
  ],
  "byes": [ { "bracket": "W", "round": 1, "slot": { "kind": "seed", "ref": 1 } } ]
}
```

A slot's `kind` is `seed` (a concrete team number, used for first-round winners games),
`winnerOf` (the winner of the referenced game), or `loserOf` (the loser of it). Downstream
participants are not concrete until earlier games are played, so they are expressed as references.

## Build & test

```bash
dotnet build Brackets.slnx
dotnet test  Brackets.slnx
```

The test suite verifies, for every team count 8–16: structural validity, the `3N−3` game count,
exactly one champion, the ≥3-games guarantee, at-most-one-bye, contiguous sequence numbers,
hundreds of random play-outs (one champion, no team double-booked within a round, ≤1 bye), that
*any* team can win, that a team can win after losing its first two games, JSON round-tripping, and
that the PDF renders.

## Library usage

```csharp
using Brackets.Core.Generation;
using Brackets.Core.Json;
using Brackets.Core.Models;
using Brackets.Pdf;

var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = 12 });
string json = BracketJson.Serialize(bracket);
PdfBracketRenderer.Save(bracket, "bracket.pdf");
```

QuestPDF is used under its free **Community** license (set automatically by the renderer); it is
free for individuals, FOSS projects, and organizations under \$1M in annual revenue.
