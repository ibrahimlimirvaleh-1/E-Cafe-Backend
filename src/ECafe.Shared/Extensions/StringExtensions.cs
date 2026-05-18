using System.Text.RegularExpressions;

namespace ECafe.Shared.Extensions
{
    public static class StringExtensions
    {
        public static string GenerateSlug(this string value)
        {
            return value
                .ToLowerInvariant()
                .Pipe(s => Regex.Replace(s, @"\s+", "-"))
                .Pipe(s => Regex.Replace(s, @"[^a-z0-9\-]", ""))
                .Trim('-');
        }

        private static string Pipe(this string s, Func<string, string> func) => func(s);
    }
}
