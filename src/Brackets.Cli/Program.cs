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
        "all" or "" => All(parser),
        "help" or "--help" or "-h" => Help(0),
        _ => Help(1, $"Unknown command '{parser.Verb}'."),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

// Default command (run with no arguments): generate every bracket size and every artifact at once.
static int All(ArgParser p)
{
    string dir = p.Get("output") ?? "out";
    Directory.CreateDirectory(dir);

    int brackets = 0;
    for (int teams = BracketOptions.MinTeams; teams <= BracketOptions.MaxTeams; teams++)
    {
        var bracket = BracketGenerator.Generate(new BracketOptions { TeamCount = teams });

        string jsonPath = Path.Combine(dir, $"bracket-{teams}.json");
        string diagramPath = Path.Combine(dir, $"bracket-{teams}-diagram.pdf");
        string sheetPath = Path.Combine(dir, $"bracket-{teams}-sheet.pdf");

        File.WriteAllText(jsonPath, BracketJson.Serialize(bracket));
        BracketDiagramRenderer.Save(bracket, diagramPath);   // one-page left-to-right diagram
        PdfBracketRenderer.Save(bracket, sheetPath);          // multi-page fillable game sheet

        Console.WriteLine($"{teams,2} teams -> {Path.GetFileName(jsonPath)}, {Path.GetFileName(diagramPath)}, {Path.GetFileName(sheetPath)}");
        brackets++;
    }

    Console.WriteLine($"Done. {brackets} brackets x (JSON + diagram + sheet) = {brackets * 3} files in '{dir}{Path.DirectorySeparatorChar}'.");
    return 0;
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
        SavePdf(p, bracket, pdfPath);
        Console.WriteLine($"Wrote PDF   -> {pdfPath}  ({PdfStyle(p)})");
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

    SavePdf(p, bracket, outPath);
    Console.WriteLine($"Wrote PDF -> {outPath}  ({PdfStyle(p)})");
    return 0;
}

static string PdfStyle(ArgParser p)
{
    // Default to the single-page, content-sized left-to-right diagram; opt in to the
    // multi-page hand-fillable game sheet with --style sheet.
    string style = (p.Get("style") ?? "diagram").ToLowerInvariant();
    return style is "sheet" or "card" ? "sheet" : "diagram";
}

static void SavePdf(ArgParser p, Bracket bracket, string path)
{
    if (PdfStyle(p) == "diagram")
    {
        BracketDiagramRenderer.Save(bracket, path);
    }
    else
    {
        PdfBracketRenderer.Save(bracket, path);
    }
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
  brackets                                (default: same as 'all')
  brackets all      [--output <dir>]
  brackets generate --teams <8-16> [--json <path>] [--pdf <path>] [--style <diagram|sheet>] [--names <file>]
  brackets pdf      (--teams <8-16> | --input <bracket.json>) --output <path> [--style <diagram|sheet>]
  brackets validate (--teams <8-16> | --input <bracket.json>)
  brackets help

OPTIONS
  --teams <n>     Number of teams (8-16).
  --json <path>   Write the bracket as JSON. (generate)
  --pdf <path>    Write a PDF. (generate)
  --output <p>    'all': output directory (default 'out'). 'pdf': output file path.
  --style <s>     PDF layout: 'diagram' (default) draws the whole traditional left-to-right
                  bracket on ONE page sized to fit it (not a standard paper size); 'sheet'
                  is a per-round, hand-fillable game sheet on Letter paper (multiple pages).
  --input <path>  Read a previously generated bracket JSON instead of generating.
  --names <file>  Text file of team names, one per line, assigned to seeds 1..n.

EXAMPLES
  brackets                              (build everything: every size 8-16, JSON + both PDFs, into out/)
  brackets all --output dist            (same, into dist/)
  brackets generate --teams 12 --json bracket.json --pdf bracket.pdf
  brackets generate --teams 16          (prints JSON to stdout)
  brackets pdf --teams 16 --output bracket.pdf             (one-page diagram, default)
  brackets pdf --teams 10 --output sheet.pdf --style sheet (multi-page fillable sheet)");

    return exitCode;
}
