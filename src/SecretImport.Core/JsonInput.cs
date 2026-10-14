using System.Text;

namespace SecretImport.Core;

/// <param name="DisplayName">Where the JSON came from (full file path, or e.g. "stdin"). Shown to the user.</param>
/// <param name="Content">The raw JSON text.</param>
public sealed record JsonInput(string DisplayName, string Content)
{
    /// <summary>Reads a JSON file as UTF-8 (a BOM, if present, is stripped).</summary>
    /// <exception cref="ImportException">
    /// <see cref="ImportErrorKind.InvalidInput"/> when the file or its directory does not exist;
    /// <see cref="ImportErrorKind.IoFailure"/> when the file cannot be read.
    /// </exception>
    public static JsonInput FromFile(string path)
    {
        string fullPath = Path.GetFullPath(path);
        try
        {
            return new JsonInput(fullPath, File.ReadAllText(fullPath, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"JSON file not found: {fullPath}", innerException: ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(ImportErrorKind.IoFailure, $"Cannot read JSON file: {fullPath}", innerException: ex);
        }
    }
}
