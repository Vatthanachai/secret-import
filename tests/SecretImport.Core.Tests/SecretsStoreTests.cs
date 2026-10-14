using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration.UserSecrets;

namespace SecretImport.Core.Tests;

public sealed class SecretsStoreTests : IDisposable
{
    private const string Id = "test-id";
    private readonly string _root = Directory.CreateTempSubdirectory("secret-import-store-").FullName;
    private readonly SecretsStore _store;

    public SecretsStoreTests()
    {
        _store = new SecretsStore(id => Path.Combine(_root, "usersecrets", id, "secrets.json"));
    }

    private string SecretsPath => Path.Combine(_root, "usersecrets", Id, "secrets.json");

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private void WriteExisting(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath)!);
        File.WriteAllText(SecretsPath, content);
    }

    private static Dictionary<string, string> D(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void DefaultResolver_IsPathHelper()
    {
        SecretsStore store = new();

        Assert.Equal(PathHelper.GetSecretsPathFromSecretsId("abc-123"), store.GetSecretsPath("abc-123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("a/b")]
    [InlineData("a\0b")]
    public void GetSecretsPath_InvalidId_IsInvalidInput(string id)
    {
        ImportException ex = Assert.Throws<ImportException>(() => _store.GetSecretsPath(id));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
    }

    [Fact]
    public void GetSecretsPath_NoUserSecretsRoot_IsIoFailure()
    {
        SecretsStore store = new(static _ => throw new InvalidOperationException("Could not determine an appropriate location for storing user secrets."));

        ImportException ex = Assert.Throws<ImportException>(() => store.GetSecretsPath(Id));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void Read_MissingFile_IsEmpty_WithNoHash()
    {
        SecretsSnapshot snapshot = _store.Read(Id);

        Assert.Equal(SecretsPath, snapshot.Path);
        Assert.Empty(snapshot.Values);
        Assert.Null(snapshot.Hash);
        Assert.False(snapshot.Exists);
        Assert.Empty(snapshot.Warnings);
    }

    [Fact]
    public void Read_FlatFile_ReturnsValues_AndHash()
    {
        WriteExisting("""{ "A:B": "1", "C": "2" }""");

        SecretsSnapshot snapshot = _store.Read(Id);

        Assert.Equal("1", snapshot.Values["a:b"]);
        Assert.Equal("2", snapshot.Values["C"]);
        Assert.True(snapshot.Exists);
        Assert.Equal(SecretsStore.ComputeHash(File.ReadAllBytes(SecretsPath)), snapshot.Hash);
    }

    [Fact]
    public void Read_NestedFile_IsFlattened()
    {
        WriteExisting("""{ "A": { "B": "1" }, "L": [ "x" ] }""");

        SecretsSnapshot snapshot = _store.Read(Id);

        Assert.Equal("1", snapshot.Values["A:B"]);
        Assert.Equal("x", snapshot.Values["L:0"]);
    }

    [Fact]
    public void Read_FileWithBom_IsRead()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsPath)!);
        File.WriteAllText(SecretsPath, """{ "A": "1" }""", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal("1", _store.Read(Id).Values["A"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Read_EmptyFile_IsEmpty_ButHasAHash(string content)
    {
        WriteExisting(content);

        SecretsSnapshot snapshot = _store.Read(Id);

        Assert.Empty(snapshot.Values);
        Assert.NotNull(snapshot.Hash);
        Assert.True(snapshot.Exists);
    }

    [Theory]
    [InlineData("{ \"A\": topsecret ")]
    [InlineData("[ \"topsecret\" ]")]
    [InlineData("{ \"A\": \"topsecret\", \"a\": \"topsecret\" }")]
    public void Read_CorruptFile_IsIoFailure_WithoutValues(string content)
    {
        WriteExisting(content);

        ImportException ex = Assert.Throws<ImportException>(() => _store.Read(Id));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.Contains(SecretsPath, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("topsecret", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_UnreadableFile_IsIoFailure()
    {
        // A directory where the file should be makes File.ReadAllBytes fail with UnauthorizedAccessException / IOException.
        Directory.CreateDirectory(SecretsPath);

        ImportException ex = Assert.Throws<ImportException>(() => _store.Read(Id));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
    }

    [Fact]
    public void Write_NewFile_CreatesFolder_AndWritesFlatIndentedJson_WithoutBackup()
    {
        SecretsWriteResult result = _store.Write(Id, D(("A:B", "1"), ("Unicode", "สวัสดี")), expectedHash: null);

        Assert.Equal(SecretsPath, result.Path);
        Assert.Null(result.BackupPath);
        string text = File.ReadAllText(SecretsPath);
        Assert.Contains("\n  \"A:B\": \"1\"", text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("สวัสดี", text, StringComparison.Ordinal);
        Assert.False(File.ReadAllBytes(SecretsPath).AsSpan().StartsWith(Encoding.UTF8.Preamble));
        using JsonDocument doc = JsonDocument.Parse(text);
        Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
        Assert.False(File.Exists(SecretsPath + ".bak"));
    }

    [Fact]
    public void Write_RoundTripsThroughRead()
    {
        Dictionary<string, string> values = D(("A", "x\"y\\z\n"), ("B", ""), ("C:D:0", "<&>+"));

        _store.Write(Id, values, expectedHash: null);
        SecretsSnapshot snapshot = _store.Read(Id);

        Assert.Equal(values, snapshot.Values);
    }

    [Fact]
    public void Write_ExistingFile_CreatesBackupOfThePreviousContent_AndReplacesTheFile()
    {
        WriteExisting("""{ "Old": "1" }""");
        string hash = _store.Read(Id).Hash!;

        SecretsWriteResult result = _store.Write(Id, D(("New", "2")), hash);

        Assert.Equal(SecretsPath + ".bak", result.BackupPath);
        Assert.Equal("""{ "Old": "1" }""", File.ReadAllText(result.BackupPath!));
        Assert.Equal(D(("New", "2")), _store.Read(Id).Values);
    }

    [Fact]
    public void Write_KeepsOnlyTheLatestBackup()
    {
        WriteExisting("""{ "V": "1" }""");
        _store.Write(Id, D(("V", "2")), _store.Read(Id).Hash);

        _store.Write(Id, D(("V", "3")), _store.Read(Id).Hash);

        Assert.Equal("2", JsonDocument.Parse(File.ReadAllText(SecretsPath + ".bak")).RootElement.GetProperty("V").GetString());
        Assert.Equal(["secrets.json", "secrets.json.bak"], Directory.GetFiles(Path.GetDirectoryName(SecretsPath)!).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Write_LeavesNoTemporaryFiles()
    {
        _store.Write(Id, D(("A", "1")), expectedHash: null);

        Assert.Equal(["secrets.json"], Directory.GetFiles(Path.GetDirectoryName(SecretsPath)!).Select(Path.GetFileName));
    }

    [Fact]
    public void Write_WhenFileChangedSinceRead_IsConcurrentModification_AndLeavesFileAlone()
    {
        WriteExisting("""{ "A": "1" }""");
        string hash = _store.Read(Id).Hash!;
        File.WriteAllText(SecretsPath, """{ "A": "edited elsewhere" }""");

        ImportException ex = Assert.Throws<ImportException>(() => _store.Write(Id, D(("A", "2")), hash));

        Assert.Equal(ImportErrorKind.ConcurrentModification, ex.Kind);
        Assert.Equal("""{ "A": "edited elsewhere" }""", File.ReadAllText(SecretsPath));
        Assert.False(File.Exists(SecretsPath + ".bak"));
    }

    [Fact]
    public void Write_WhenFileAppearedSinceRead_IsConcurrentModification()
    {
        WriteExisting("""{ "A": "1" }""");

        ImportException ex = Assert.Throws<ImportException>(() => _store.Write(Id, D(("A", "2")), expectedHash: null));

        Assert.Equal(ImportErrorKind.ConcurrentModification, ex.Kind);
    }

    [Fact]
    public void Write_WhenFileDisappearedSinceRead_IsConcurrentModification()
    {
        ImportException ex = Assert.Throws<ImportException>(() => _store.Write(Id, D(("A", "2")), expectedHash: "ABC"));

        Assert.Equal(ImportErrorKind.ConcurrentModification, ex.Kind);
        Assert.False(File.Exists(SecretsPath));
    }

    [Fact]
    public void Write_IoError_IsIoFailure_AndLeavesNothingBehind()
    {
        // A file sits where the <id> folder should be, so the folder cannot be created.
        string idFolder = Path.GetDirectoryName(SecretsPath)!;
        Directory.CreateDirectory(Path.GetDirectoryName(idFolder)!);
        File.WriteAllText(idFolder, "not a folder");

        ImportException ex = Assert.Throws<ImportException>(() => _store.Write(Id, D(("A", "1")), expectedHash: null));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.Equal("not a folder", File.ReadAllText(idFolder));
        Assert.Equal([idFolder], Directory.GetFileSystemEntries(Path.GetDirectoryName(idFolder)!));
    }

    [Fact]
    public void ComputeHash_IsStableUppercaseHexSha256()
    {
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", SecretsStore.ComputeHash("abc"u8.ToArray()));
    }

    // ---- Unix permissions (ADR-0009); skipped on Windows ----

    private static void SkipOnWindows() => Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not apply on Windows.");

    [Fact]
    public void Unix_NewFile_Is600_AndCreatedFolderIs700()
    {
        SkipOnWindows();
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _store.Write(Id, D(("A", "1")), expectedHash: null);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SecretsPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.GetDirectoryName(SecretsPath)!));
    }

    [Fact]
    public void Unix_ExistingFile_KeepsItsMode_AndBackupGetsTheSameMode()
    {
        SkipOnWindows();
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead;
        WriteExisting("""{ "A": "1" }""");
        File.SetUnixFileMode(SecretsPath, mode);

        SecretsWriteResult result = _store.Write(Id, D(("A", "2")), _store.Read(Id).Hash);

        Assert.Equal(mode, File.GetUnixFileMode(SecretsPath));
        Assert.Equal(mode, File.GetUnixFileMode(result.BackupPath!));
    }

    [Fact]
    public void Unix_GroupOrOtherReadable_ProducesAWarning_WithoutChangingTheMode()
    {
        SkipOnWindows();
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        WriteExisting("""{ "A": "1" }""");
        File.SetUnixFileMode(SecretsPath, mode);

        SecretsSnapshot snapshot = _store.Read(Id);

        ImportWarning warning = Assert.Single(snapshot.Warnings);
        Assert.Equal(ImportWarningKind.InsecureFilePermissions, warning.Kind);
        Assert.Contains("644", warning.Message, StringComparison.Ordinal);
        Assert.Equal(mode, File.GetUnixFileMode(SecretsPath));
    }

    [Fact]
    public void Unix_600File_HasNoWarning()
    {
        SkipOnWindows();
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        WriteExisting("""{ "A": "1" }""");
        File.SetUnixFileMode(SecretsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Assert.Empty(_store.Read(Id).Warnings);
    }
}