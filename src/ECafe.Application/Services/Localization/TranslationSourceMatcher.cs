using System.Text;
using System.Text.RegularExpressions;

namespace ECafe.Application.Services.Localization;

public sealed record TranslationMatch(string Key, IReadOnlyDictionary<string, object?> Arguments);

public sealed partial class TranslationSourceMatcher
{
    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private readonly List<(Regex Pattern, string Key, string[] Names, string Prefix, string Suffix)> _templates = [];

    /// <summary>Yalnız build-də açıq qeydə alınmış köhnə sistem mətnlərini sabit açarlara uyğunlaşdırır; admin mətni regex-ə və ya koda çevrilmir.</summary>
    public TranslationSourceMatcher(IEnumerable<KeyValuePair<string, string>> sources)
    {
        foreach (var pair in sources)
        {
            var matches = Placeholder().Matches(pair.Value);
            if (matches.Count == 0) { _exact.TryAdd(pair.Value, pair.Key); continue; }
            var pattern = new StringBuilder("\\A");
            var names = new List<string>();
            var cursor = 0;
            foreach (Match match in matches)
            {
                pattern.Append(Regex.Escape(pair.Value[cursor..match.Index]));
                pattern.Append("(?<p").Append(names.Count).Append(">.*?)");
                names.Add(match.Groups[1].Value);
                cursor = match.Index + match.Length;
            }
            pattern.Append(Regex.Escape(pair.Value[cursor..])).Append("\\z");
            _templates.Add((new Regex(pattern.ToString(), RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.NonBacktracking,
                TimeSpan.FromMilliseconds(50)), pair.Key, names.ToArray(),
                pair.Value[..matches[0].Index], pair.Value[(matches[^1].Index + matches[^1].Length)..]));
        }
    }

    /// <summary>Yalnız tam uyğun sistem şablonunu tanıyır; sabit kənarları əvvəlcədən yoxlayır, regex vaxtı bitərsə ilkin mətnin saxlanması üçün uyğunluq qaytarmır.</summary>
    public TranslationMatch? Match(string source)
    {
        if (source.Length > 16000) return null;
        if (_exact.TryGetValue(source, out var key)) return new(key, new Dictionary<string, object?>());
        foreach (var template in _templates)
        {
            if (source.Length < template.Prefix.Length + template.Suffix.Length ||
                !source.StartsWith(template.Prefix, StringComparison.Ordinal) ||
                !source.EndsWith(template.Suffix, StringComparison.Ordinal)) continue;

            Match match;
            try
            {
                match = template.Pattern.Match(source);
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
            if (!match.Success) continue;
            var arguments = new Dictionary<string, object?>(StringComparer.Ordinal);
            var compatible = true;
            for (var index = 0; index < template.Names.Length; index++)
            {
                var value = match.Groups["p" + index].Value;
                if (arguments.TryGetValue(template.Names[index], out var previous) && !Equals(previous, value)) { compatible = false; break; }
                arguments[template.Names[index]] = value;
            }
            if (compatible) return new(template.Key, arguments);
        }
        return null;
    }

    /// <summary>Yalnız sadə adlı parametr yuvalarını tanıyır; mətnin digər hissələri bütöv literal sayılır.</summary>
    [GeneratedRegex(@"\{\{?\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}?", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
