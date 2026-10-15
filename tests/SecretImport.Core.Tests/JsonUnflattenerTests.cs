using System.Text;

using Microsoft.Extensions.Configuration;

namespace SecretImport.Core.Tests;

public class JsonUnflattenerTests
{
    private readonly JsonUnflattener _unflattener = new();

    private static KeyValuePair<string, string>[] Pairs(params (string Key, string Value)[] pairs) =>
        [.. pairs.Select(static p => KeyValuePair.Create(p.Key, p.Value))];

    /// <summary>Builds the expected document from lines, joined with '\n' plus a trailing newline.</summary>
    private static string Lines(params string[] lines) => string.Join('\n', lines) + "\n";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoKeys_IsAnEmptyObject(bool flat)
    {
        Assert.Equal("{}\n", _unflattener.ToJson([], flat));
    }

    [Fact]
    public void Flat_WritesKeysAsStored_InStoreOrder()
    {
        string json = _unflattener.ToJson(Pairs(("Db:Conn", "c"), ("Name", "n"), ("db:port", "5432")), flat: true);

        Assert.Equal(
            Lines("{", "  \"Db:Conn\": \"c\",", "  \"Name\": \"n\",", "  \"db:port\": \"5432\"", "}"),
            json);
    }

    [Fact]
    public void Flat_NeverConflicts()
    {
        string json = _unflattener.ToJson(Pairs(("A", "1"), ("A:B", "2")), flat: true);

        Assert.Equal(Lines("{", "  \"A\": \"1\",", "  \"A:B\": \"2\"", "}"), json);
    }

    [Fact]
    public void Nested_SplitsOnColons_IntoObjects_InFirstSeenOrder()
    {
        string json = _unflattener.ToJson(Pairs(("Db:Conn", "c"), ("Name", "n"), ("Db:Opt:Retry", "3")), flat: false);

        Assert.Equal(
            Lines(
                "{",
                "  \"Db\": {",
                "    \"Conn\": \"c\",",
                "    \"Opt\": {",
                "      \"Retry\": \"3\"",
                "    }",
                "  },",
                "  \"Name\": \"n\"",
                "}"),
            json);
    }

    [Fact]
    public void Nested_GroupsCaseInsensitively_KeepingTheFirstSpelling()
    {
        string json = _unflattener.ToJson(Pairs(("Db:Conn", "c"), ("DB:Port", "1"), ("db:OPT:x", "2")), flat: false);

        Assert.Equal(
            Lines("{", "  \"Db\": {", "    \"Conn\": \"c\",", "    \"Port\": \"1\",", "    \"OPT\": {", "      \"x\": \"2\"", "    }", "  }", "}"),
            json);
    }

    [Fact]
    public void Nested_ContiguousIndexes_BecomeAnArray_InIndexOrder()
    {
        string json = _unflattener.ToJson(Pairs(("Items:1", "b"), ("Items:0", "a"), ("Items:2:Name", "c")), flat: false);

        Assert.Equal(
            Lines("{", "  \"Items\": [", "    \"a\",", "    \"b\",", "    {", "      \"Name\": \"c\"", "    }", "  ]", "}"),
            json);
    }

    [Theory]
    [InlineData("1")]          // does not start at 0
    [InlineData("0", "2")]     // gap
    [InlineData("0", "01")]    // not canonical
    [InlineData("0", "x")]     // not a number
    [InlineData("-1", "0")]    // negative
    public void Nested_OtherChildNames_StayAnObject(params string[] names)
    {
        string json = _unflattener.ToJson([.. names.Select(static n => KeyValuePair.Create("Items:" + n, "v"))], flat: false);

        Assert.StartsWith(Lines("{", "  \"Items\": {").TrimEnd('\n'), json, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_Root_IsAlwaysAnObject_EvenWithIndexKeys()
    {
        string json = _unflattener.ToJson(Pairs(("0", "a"), ("1", "b")), flat: false);

        Assert.Equal(Lines("{", "  \"0\": \"a\",", "  \"1\": \"b\"", "}"), json);
    }

    [Fact]
    public void Values_AreAlwaysStrings_AndOnlyJsonRequiredCharactersAreEscaped()
    {
        string json = _unflattener.ToJson(
            Pairs(("Port", "5432"), ("On", "true"), ("Empty", string.Empty), ("Thai", "รหัสผ่าน"), ("Html", "<a&b+'c'>"), ("Quote", "say \"hi\"\\\t")),
            flat: false);

        Assert.Equal(
            Lines(
                "{",
                "  \"Port\": \"5432\",",
                "  \"On\": \"true\",",
                "  \"Empty\": \"\",",
                "  \"Thai\": \"รหัสผ่าน\",",
                "  \"Html\": \"<a&b+'c'>\",",
                "  \"Quote\": \"say \\\"hi\\\"\\\\\\t\"",
                "}"),
            json);
    }

    [Fact]
    public void EmptySegments_AreKeptAsEmptyPropertyNames()
    {
        string json = _unflattener.ToJson(Pairs(("A:", "x"), (string.Empty, "y")), flat: false);

        Assert.Equal(Lines("{", "  \"A\": {", "    \"\": \"x\"", "  },", "  \"\": \"y\"", "}"), json);
    }

    [Theory]
    [InlineData("A", "A:B", "A", "A:B")]          // value first
    [InlineData("A:B", "a", "a", "A:B")]          // children first, different case
    [InlineData("X:A:B:C", "x:a", "x:a", "X:A:B:C")] // deep
    [InlineData("X:A", "X:A:B", "X:A", "X:A:B")]
    public void Nested_ValueAndChildrenOnTheSameKey_IsInvalidInput_NamingBothKeys_AndSuggestingFlat(string first, string second, string valueKey, string childKey)
    {
        ImportException ex = Assert.Throws<ImportException>(() =>
            _unflattener.ToJson(Pairs((first, "secret-1"), (second, "secret-2")), flat: false));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains($"'{valueKey}'", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"'{childKey}'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--flat", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_SameKeyTwice_IsInvalidInput_WithoutValues()
    {
        // The store never yields this (it is case-insensitive), but a caller's dictionary might.
        ImportException ex = Assert.Throws<ImportException>(() =>
            _unflattener.ToJson(Pairs(("Db:Conn", "secret-1"), ("DB:CONN", "secret-2")), flat: false));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("'Db:Conn'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("'DB:CONN'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("--flat", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Golden_ConfigurationJsonProvider_ReadsBackTheSameValues(bool flat)
    {
        KeyValuePair<string, string>[] values = Pairs(
            ("Db:Conn", "Server=.;Password=p@ss"),
            ("DB:Port", "5432"),
            ("Items:0", "a"),
            ("Items:1:Name", "b"),
            ("Items:1:Tags:0", "t"),
            ("Thai", "รหัส"),
            ("Empty", string.Empty),
            ("Sparse:0", "x"),
            ("Sparse:5", "y"));

        string json = _unflattener.ToJson(values, flat);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();

        // Configuration keys are case-insensitive, so compare by lookup rather than by spelling.
        Assert.Equal(values.Length, configuration.AsEnumerable().Count(static p => p.Value is not null));
        Assert.All(values, p => Assert.Equal(p.Value, configuration[p.Key]));
    }
}