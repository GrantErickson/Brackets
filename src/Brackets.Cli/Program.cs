using Brackets.Cli;
using Brackets.Core.Generation;
using Brackets.Core.Json;
using Brackets.Core.Models;
using Brackets.Core.Validation;
using Brackets.Pdf;

var parser = ArgParser.Parse(args);

try
{
    return parser.Verb.ToLowerInvariant() switch
    {
        "generate" => Generate(parser),
        "pdf" => Pdf(parser),
        "validate" => Validate(parser),
        "help" or "--help" or "-h" or "" => Help(0),
        _ => Help(1, $"Unknown command '{parser.Verb}'."),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int Generate(ArgParser p)
{
    var bracket = BuildBracket(p);

    bool wroteSomething = false;

    if (p.Get("json") is string jsonPath)
    {
        File.WriteAllText(jsonPath, BracketJson.Serialize(bracket));
        Console.WriteLine($"Wrote JSON  -> {jsonPath}");
        wroteSomething = true;
    }

    if (p.Get("pdf") is string pdfPath)
    {
        PdfBracketRenderer.Save(bracket, pdfPath);
        Console.WriteLine($"Wrote PDF   -> {pdfPath}");
        wroteSomething = true;
    }

    if (!wroteSomething)
    {
        Console.WriteLine(BracketJson.Serialize(bracket));
    }
    else
    {
        Console.WriteLine($"{bracket.TeamCount} teams · {bracket.Games.Count} games · {bracket.SequenceCount} rounds.");
    }

    return 0;
}

static int Pdf(ArgParser p)
{
    string outPath = p.Get("output")
        ?? throw new ArgumentException("pdf requires --output <path>.");

    Bracket bracket = p.Get("input") is string inputPath
        ? BracketJson.Deserialize(File.ReadAllText(inputPath))
        : BuildBracket(p);

    PdfBracketRenderer.Save(bracket, outPath);
    Console.WriteLine($"Wrote PDF -> {outPath}");
    return 0;
}

static int Validate(ArgParser p)
{
    Bracket bracket = p.Get("input") is string inputPath
        ? BracketJson.Deserialize(File.ReadAllText(inputPath))
        : BuildBracket(p);

    var errors = BracketValidator.Validate(bracket);
    if (errors.Count == 0)
    {
        Console.WriteLine($"Valid: {bracket.TeamCount} teams, {bracket.Games.Count} games, every team guaranteed >= 3 games.");
        return 0;
    }

    Console.Error.WriteLine("Invalid bracket:");
    foreach (var error in errors)
    {
        Console.Error.WriteLine($"  - {error}");
    }

    return 1;
}

static Bracket BuildBracket(ArgParser p)
{
    int teams = p.GetInt("teams")
        ?? throw new ArgumentException($"--teams <{BracketOptions.MinTeams}-{BracketOptions.MaxTeams}> is required.");

    var options = new BracketOptions { TeamCount = teams };

    if (p.Get("names") is string namesPath)
    {
        var names = File.ReadAllLines(namesPath)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        options.TeamNames = new Dictionary<int, string>();
        for (int i = 0; i < names.Count && i < teams; i++)
        {
            options.TeamNames[i + 1] = names[i];
        }
    }

    return BracketGenerator.Generate(options);
}

static int Help(int exitCode, string? message = null)
{
    if (message is not null)
    {
        Console.Error.WriteLine(message);
        Console.Error.WriteLine();
    }

    Console.WriteLine(
@"brackets - three-life elimination bracket generator (8-16 teams)

A team is eliminated only on its third loss, so every team is guaranteed at least three
games and any team can climb back to win the championship. All games in a round share a
sequence number and are played at the same time.

USAGE
  brackets generate --teams <8-16> [--json <path>] [--pdf <path>] [--names <file>]
  brackets pdf      (--teams <8-16> | --input <bracket.json>) --output <path>
  brackets validate (--teams <8-16> | --input <bracket.json>)
  brackets help

OPTIONS
  --teams <n>     Number of teams (8-16).
  --json <path>   Write the bracket as JSON. (generate)
  --pdf <path>    Write a fillable PDF bracket sheet. (generate)
  --output <path> Output PDF path. (pdf)
  --input <path>  Read a previously generated bracket JSON instead of generating.
  --names <file>  Text file of team names, one per line, assigned to seeds 1..n.

EXAMPLES
  brackets generate --teams 12 --json bracket.json --pdf bracket.pdf
  brackets generate --teams 16          (prints JSON to stdout)
  brackets pdf --teams 10 --output sheet.pdf");

    return exitCode;
}
