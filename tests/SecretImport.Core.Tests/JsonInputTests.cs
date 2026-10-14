using System.Text;

namespace SecretImport.Core.Tests;

public sealed class JsonInputTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("secret-import-tests-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void FromFile_ReadsContentAsUtf8_AndUsesFullPathAsDisplayName()
    {
        string path = Path.Combine(_dir, "secrets.json");
        string content = "{ \"Greeting\": \"สวัสดี ✓\" }";
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        JsonInput input = JsonInput.FromFile(path);

        Assert.Equal(content, input.Content);
        Assert.Equal(Path.GetFullPath(path), input.DisplayName);
    }

    [Fact]
    public void FromFile_StripsUtf8Bom()
    {
        string path = Path.Combine(_dir, "bom.json");
        File.WriteAllText(path, "{}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        JsonInput input = JsonInput.FromFile(path);

        Assert.Equal("{}", input.Content);
    }

    [Fact]
    public void FromFile_MissingFile_ThrowsInvalidInput()
    {
        string path = Path.Combine(_dir, "missing.json");

        ImportException ex = Assert.Throws<ImportException>(() => JsonInput.FromFile(path));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.IsType<FileNotFoundException>(ex.InnerException);
        Assert.Contains("missing.json", ex.Message);
    }

    [Fact]
    public void FromFile_MissingDirectory_ThrowsInvalidInput()
    {
        string path = Path.Combine(_dir, "nope", "missing.json");

        ImportException ex = Assert.Throws<ImportException>(() => JsonInput.FromFile(path));

        Assert.Equal(ImportErrorKind.InvalidInput, ex.Kind);
        Assert.IsType<DirectoryNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void FromFile_UnreadablePath_ThrowsIoFailure()
    {
        ImportException ex = Assert.Throws<ImportException>(() => JsonInput.FromFile(_dir));

        Assert.Equal(ImportErrorKind.IoFailure, ex.Kind);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void Constructor_KeepsDisplayNameAndContent()
    {
        JsonInput input = new("stdin", "{}");

        Assert.Equal("stdin", input.DisplayName);
        Assert.Equal("{}", input.Content);
    }
}
