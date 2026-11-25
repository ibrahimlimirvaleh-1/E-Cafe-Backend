using System.Reflection;
using System.Text.RegularExpressions;
using ECafe.Application.Services.Localization;
using Xunit;

namespace ECafe.Tests;

public sealed class TranslationSourceMatcherTests
{
    [Theory]
    [InlineData("Order #42 confirmed.", "42")]
    [InlineData("Order # confirmed.", "")]
    [InlineData("Order #first\nsecond confirmed.", "first\nsecond")]
    [InlineData("Order #<script>{amount}</script> confirmed.", "<script>{amount}</script>")]
    public void LiteralEdgesPreserveCapturedText(string source, string expected)
    {
        var matcher = new TranslationSourceMatcher([new("order", "Order #{id} confirmed.")]);
        var match = matcher.Match(source);
        Assert.Equal("order", match!.Key);
        Assert.Equal(expected, match.Arguments["id"]);
    }

    [Theory]
    [InlineData("User said: Order #42 confirmed.")]
    [InlineData("order #42 confirmed.")]
    [InlineData("Order #42 confirmed. trailing")]
    [InlineData("Order #42 rejected.")]
    [InlineData("Order #")]
    public void UnknownTextAndPartialOrCaseInsensitiveMatchesAreRejected(string source)
    {
        var matcher = new TranslationSourceMatcher([new("order", "Order #{id} confirmed.")]);
        Assert.Null(matcher.Match(source));
    }

    [Theory]
    [InlineData("{value} suffix", "value suffix", "value")]
    [InlineData("prefix {value}", "prefix value", "value")]
    [InlineData("{value}", "anything\nelse", "anything\nelse")]
    [InlineData("[{value}].+", "[literal].+", "literal")]
    [InlineData("prefix {{ value }} suffix", "prefix value suffix", "value")]
    public void EmptyEdgesAndEscapedRegexCharactersKeepExistingSemantics(string template, string source, string expected)
    {
        var matcher = new TranslationSourceMatcher([new("template", template)]);
        Assert.Equal(expected, matcher.Match(source)!.Arguments["value"]);
    }

    [Fact]
    public void ExactTextWinsBeforeTemplatesAndTemplateOrderIsPreserved()
    {
        var matcher = new TranslationSourceMatcher([
            new("first", "Order {id}"), new("second", "Order {value}"), new("exact", "Order 42")]);
        Assert.Equal("exact", matcher.Match("Order 42")!.Key);
        Assert.Equal("first", matcher.Match("Order 43")!.Key);
    }

    [Fact]
    public void RepeatedNamesMustHaveIdenticalValuesAndAdjacentParametersStayLazy()
    {
        var matcher = new TranslationSourceMatcher([new("repeat", "{id}/{id}")]);
        Assert.Equal("42", matcher.Match("42/42")!.Arguments["id"]);
        Assert.Null(matcher.Match("42/43"));

        var adjacent = new TranslationSourceMatcher([new("adjacent", "[{first}{second}]")]);
        var match = adjacent.Match("[value]")!;
        Assert.Equal("", match.Arguments["first"]);
        Assert.Equal("value", match.Arguments["second"]);
    }

    [Fact]
    public void SourceLengthLimitStillAppliesBeforeExactAndTemplateMatching()
    {
        var oversized = new string('x', 16001);
        var matcher = new TranslationSourceMatcher([new("exact", oversized), new("template", "{value}")]);
        Assert.Null(matcher.Match(oversized));
        Assert.Equal(16000, ((string)matcher.Match(new string('x', 16000))!.Arguments["value"]!).Length);
    }

    [Fact]
    public void RegexTimeoutReturnsNoTranslationWithoutBreakingLaterRequests()
    {
        var matcher = new TranslationSourceMatcher([new("template", "{value}"), new("exact", "Known text")]);
        var templates = Templates(matcher);
        var original = templates[0];
        var timingOut = new Regex(@"\A(a+)+\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        templates[0] = (timingOut, original.Key, original.Names, original.Prefix, original.Suffix);
        var source = new string('a', 12000) + "!";

        Assert.Throws<RegexMatchTimeoutException>(() => timingOut.Match(source));
        Assert.Null(matcher.Match(source));
        Assert.Equal("exact", matcher.Match("Known text")!.Key);
        templates[0] = original;
        Assert.Equal("value", matcher.Match("value")!.Arguments["value"]);
    }

    [Fact]
    public void UnrelatedLiteralEdgesAreSkippedBeforeRegexExecution()
    {
        var matcher = new TranslationSourceMatcher([new("unrelated", "Other {value} end"), new("wanted", "{value}")]);
        var templates = Templates(matcher);
        var original = templates[0];
        templates[0] = (new Regex(@"\A(a+)+\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1)),
            original.Key, original.Names, original.Prefix, original.Suffix);
        var source = new string('a', 12000) + "!";

        Assert.Equal("wanted", matcher.Match(source)!.Key);
    }

    /// <summary>Vaxt bitməsini deterministik yaratmaq üçün yalnız testdə regex-i dəyişir; istehsal regex seçimləri və public API dəyişmir.</summary>
    private static List<(Regex Pattern, string Key, string[] Names, string Prefix, string Suffix)> Templates(TranslationSourceMatcher matcher)
        => (List<(Regex Pattern, string Key, string[] Names, string Prefix, string Suffix)>)
            typeof(TranslationSourceMatcher).GetField("_templates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(matcher)!;
}
