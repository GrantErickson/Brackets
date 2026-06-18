namespace Brackets.Cli;

/// <summary>Minimal <c>--key value</c> / <c>--flag</c> parser (no third-party dependency).</summary>
internal sealed class ArgParser
{
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public string Verb { get; }

    private ArgParser(string verb) => Verb = verb;

    public static ArgParser Parse(string[] args)
    {
        var parser = new ArgParser(args.Length > 0 ? args[0] : string.Empty);
        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = token[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                parser._options[key] = args[++i];
            }
            else
            {
                parser._flags.Add(key);
            }
        }

        return parser;
    }

    public bool Has(string key) => _flags.Contains(key) || _options.ContainsKey(key);

    public string? Get(string key) => _options.TryGetValue(key, out var value) ? value : null;

    public int? GetInt(string key) =>
        _options.TryGetValue(key, out var value) && int.TryParse(value, out int parsed) ? parsed : null;
}
