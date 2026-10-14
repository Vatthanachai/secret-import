using Microsoft.Extensions.Configuration;

namespace SecretImport.Core.Tests;

public sealed class JsonFlattenerTests
{
    private readonly JsonFlattener _flattener = new();

    private FlattenResult Flatten(string json, string? section = null) => _flattener.Flatten(json, section, "input.json");

    [Fact]
    public void NestedObjects_AreJoinedWithColon()
    {
        FlattenResult result = Flatten("""{ "Db": { "Main": { "Host": "localhost" } }, "Top": "x" }""");

        Assert.Equal("localhost", result.Values["Db:Main:Host"]);
        Assert.Equal("x", result.Values["Top"]);
        Assert.Equal(2, result.Values.Count);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Arrays_UseIndexKeys()
    {
        FlattenResult result = Flatten("""{ "Items": [ "a", { "Name": "b" }, [ "c" ] ] }""");

        Assert.Equal("a", result.Values["Items:0"]);
        Assert.Equal("b", result.Values["Items:1:Name"]);
        Assert.Equal("c", result.Values["Items:2:0"]);
    }

    [Fact]
    public void NumbersAndBooleans_AreConvertedToInvariantStrings()
    {
        FlattenResult result = Flatten("""{ "Port": 5432, "Ratio": 1.5, "Big": 1e3, "On": true, "Off": false }""");

        Assert.Equal("5432", result.Values["Port"]);
        Assert.Equal("1.5", result.Values["Ratio"]);
        Assert.Equal("1e3", result.Values["Big"]);
        Assert.Equal("true", result.Values["On"]);
        Assert.Equal("false", result.Values["Off"]);
    }

    [Fact]
    public void NullValues_AreSkipped_WithAWarningThatNamesTheKey()
    {
        FlattenResult result = Flatten("""{ "A": null, "B": { "C": null }, "D": "x" }""");

        Assert.Equal(["D"], result.Values.Keys);
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, w => Assert.Equal(ImportWarningKind.NullValueSkipped, w.Kind));
        Assert.Contains(result.Warnings, w => w.Message.Contains("'A'", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, w => w.Message.Contains("'B:C'", StringComparison.Ordinal));
    }

    [Fact]
    public void EmptyString_IsImported()
    {
        FlattenResult result = Flatten("""{ "Empty": "" }""");

        Assert.Equal(string.Empty, result.Values["Empty"]);
    }

    [Fact]
    public void EmptyObjectsAndArrays_ProduceNoKeys()
    {
        FlattenResult result = Flatten("""{ "O": {}, "A": [], "K": "v" }""");

        Assert.Equal(["K"], result.Values.Keys);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void EmptyRootObject_ProducesNoKeys()
    {
        FlattenResult result = Flatten("{}");

        Assert.Empty(result.Values);
    }

    [Fact]
    public void Values_AreCaseInsensitive()
    {
        FlattenResult result = Flatten("""{ "Key": "v" }""");

        Assert.Equal("v", result.Values["KEY"]);
    }

    [Fact]
    public void Values_KeepDocumentOrder()
    {
        FlattenResult result = Flatten("""{ "Z": "1", "A": { "Y": "2", "B": "3" } }""");

        Assert.Equal(["Z", "A:Y", "A:B"], result.Values.Keys);
    }

    [Fact]
    public void Jsonc_CommentsAndTrailingCommas_AreAccepted()
    {
        const string json = """
            {
              // line comment
              "A": "1", /* block comment */
              "B": [ "x", "y", ],
            }
            """;

        FlattenResult result = Flatten(json);

        Assert.Equal("1", result.Values["A"]);
        Assert.Equal("y", result.Values["B:1"]);
    }

    [Theory]
    [InlineData("""{ "A": { "B": "1" }, "A:B": "2" }""", "A:B")]
    [InlineData("""{ "Key": "1", "KEY": "2" }""", "KEY")]
    [InlineData("""{ "Key": "1", "Key": "2" }""", "Key")]
    public void DuplicateKeysAfterFlattening_AreAnError_NamingTheKey(string json, string key)
    {
        ImportException ex = Assert.Throws<ImportException>(() => Flatten(json));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains($"'{key}'", ex.Message, StringComparison.Ordinal);
        Assert.Contains("input.json", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateKey_ErrorMessage_DoesNotContainSecretValues()
    {
        ImportException ex = Assert.Throws<ImportException>(() => Flatten("""{ "K": "secret-one", "k": "secret-two" }"""));

        Assert.DoesNotContain("secret-one", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-two", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[ 1, 2 ]")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void RootThatIsNotAnObject_IsAnError(string json)
    {
        ImportException ex = Assert.Throws<ImportException>(() => Flatten(json));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("object", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ \"A\": supersecret }")]
    [InlineData("{ \"A\": \"unterminated")]
    public void MalformedJson_IsAnError_WithoutEchoingContent(string json)
    {
        ImportException ex = Assert.Throws<ImportException>(() => Flatten(json));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("input.json", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unterminated", ex.Message, StringComparison.Ordinal);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void Section_SelectsOnlyThatSection_AndKeepsThePrefix()
    {
        FlattenResult result = Flatten("""{ "ConnectionStrings": { "Default": "cs" }, "Other": "x", "ConnectionStringsX": "y" }""", "ConnectionStrings");

        Assert.Equal(["ConnectionStrings:Default"], result.Values.Keys);
        Assert.Equal("cs", result.Values["ConnectionStrings:Default"]);
    }

    [Fact]
    public void Section_IsCaseInsensitive_AndSupportsNestedPaths()
    {
        FlattenResult result = Flatten("""{ "Logging": { "LogLevel": { "Default": "Info" }, "Other": "x" } }""", "logging:loglevel");

        Assert.Equal(["Logging:LogLevel:Default"], result.Values.Keys);
    }

    [Fact]
    public void Section_ThatIsALeafValue_SelectsThatKey()
    {
        FlattenResult result = Flatten("""{ "ApiKey": "k", "Other": "x" }""", "ApiKey");

        Assert.Equal(["ApiKey"], result.Values.Keys);
    }

    [Fact]
    public void Section_OnlyReportsNullWarningsInsideTheSection()
    {
        FlattenResult result = Flatten("""{ "S": { "A": null, "B": "b" }, "Outside": null }""", "S");

        ImportWarning warning = Assert.Single(result.Warnings);
        Assert.Contains("'S:A'", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "Other": "x" }""")]
    [InlineData("""{ "S": { "A": null } }""")]
    [InlineData("""{ "S": {} }""")]
    public void Section_NotFound_IsAnError(string json)
    {
        ImportException ex = Assert.Throws<ImportException>(() => Flatten(json, "S"));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.Contains("'S'", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Section_NullOrBlank_MeansWholeDocument(string? section)
    {
        FlattenResult result = Flatten("""{ "A": "1", "B": "2" }""", section);

        Assert.Equal(2, result.Values.Count);
    }

    [Fact]
    public void Section_SurroundingWhitespaceAndColons_AreIgnored()
    {
        FlattenResult result = Flatten("""{ "S": { "A": "1" } }""", " S: ");

        Assert.Equal(["S:A"], result.Values.Keys);
    }

    [Fact]
    public void Golden_MatchesConfigurationBuilderAddJsonFile()
    {
        const string json = """
            {
              // JSONC is accepted by both
              "ConnectionStrings": { "Default": "Server=.;Database=App;" },
              "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
              "Port": 5432,
              "Ratio": -0.25,
              "Exp": 6.02e23,
              "Hosts": [ "a", "b", { "Name": "c", "Tags": [ 1, 2 ] } ],
              "Empty": "",
              "Unicode": "สวัสดี ✓ é",
              "Escaped": "line\nbreak \"quoted\" \\ slash",
              "Deep": { "A": { "B": { "C": { "D": "deep" } } } },
            }
            """;
        string dir = Directory.CreateTempSubdirectory("secret-import-golden-").FullName;
        try
        {
            string path = Path.Combine(dir, "appsettings.json");
            File.WriteAllText(path, json);
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(path).Build();
            Dictionary<string, string?> expected = config.AsEnumerable()
                .Where(static p => p.Value is not null)
                .ToDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);

            FlattenResult result = Flatten(json);

            Assert.Equal(expected.Count, result.Values.Count);
            foreach ((string key, string value) in result.Values)
            {
                Assert.True(expected.TryGetValue(key, out string? configValue), $"Configuration has no key '{key}'.");
                Assert.True(string.Equals(configValue, value, StringComparison.Ordinal), $"Value mismatch for '{key}'.");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Golden_BooleansDifferFromConfigurationBuilderOnlyByCase()
    {
        // ConfigurationBuilder stores JSON booleans as "True"/"False" (JsonElement.ToString()).
        // ADR-0004 asks for invariant JSON text ("true"); binding to bool is case-insensitive either way.
        string dir = Directory.CreateTempSubdirectory("secret-import-golden-").FullName;
        try
        {
            string path = Path.Combine(dir, "appsettings.json");
            File.WriteAllText(path, """{ "On": true }""");
            IConfigurationRoot config = new ConfigurationBuilder().AddJsonFile(path).Build();

            FlattenResult result = Flatten("""{ "On": true }""");

            Assert.Equal(config["On"], result.Values["On"], ignoreCase: true);
            Assert.True(bool.Parse(config["On"]!));
            Assert.True(bool.Parse(result.Values["On"]));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}