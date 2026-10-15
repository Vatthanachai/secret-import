using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.Extensions.Configuration.UserSecrets;

namespace SecretImport.Core;

/// <summary>The stored secrets of one UserSecretsId as read from disk.</summary>
/// <param name="Path">The full path of <c>secrets.json</c>.</param>
/// <param name="Values">The flattened secrets (empty when the file does not exist).</param>
/// <param name="Hash">SHA-256 of the file bytes, or null when the file does not exist.</param>
/// <param name="Warnings">E.g. <see cref="ImportWarningKind.InsecureFilePermissions"/>.</param>
internal sealed record SecretsSnapshot(string Path, IReadOnlyDictionary<string, string> Values, string? Hash, IReadOnlyList<ImportWarning> Warnings)
{
    public bool Exists => Hash is not null;
}

internal sealed record SecretsWriteResult(string Path, string? BackupPath);

/// <summary>
/// Reads and writes <c>secrets.json</c> directly (ADR-0002): the path comes from <see cref="PathHelper"/>,
/// writes are atomic (temp file + rename) after a <c>.bak</c> copy, and Unix modes follow ADR-0009.
/// </summary>
internal sealed class SecretsStore
{
    private const UnixFileMode DirectoryMode700 = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode GroupOrOther = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        // The file is never embedded in HTML/JS, so keep non-ASCII and '+', '<', '&' readable like the official tool does.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Func<string, string> _resolvePath;
    private readonly JsonFlattener _flattener = new();

    public SecretsStore()
        : this(PathHelper.GetSecretsPathFromSecretsId)
    {
    }

    /// <param name="resolvePath">Maps a UserSecretsId to the full path of its <c>secrets.json</c> (tests point this at a temp dir).</param>
    public SecretsStore(Func<string, string> resolvePath)
    {
        _resolvePath = resolvePath;
    }

    public string GetSecretsPath(string userSecretsId)
    {
        if (string.IsNullOrWhiteSpace(userSecretsId) || userSecretsId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ImportException(ImportErrorKind.InvalidInput, $"UserSecretsId '{userSecretsId.Replace("\0", string.Empty, StringComparison.Ordinal)}' is empty or contains characters that are not valid in a file name.");
        }

        try
        {
            return _resolvePath(userSecretsId);
        }
        catch (InvalidOperationException ex)
        {
            throw new ImportException(ImportErrorKind.IoFailure, ex.Message, innerException: ex);
        }
    }

    public SecretsSnapshot Read(string userSecretsId)
    {
        string path = GetSecretsPath(userSecretsId);
        byte[]? bytes = ReadBytes(path);
        if (bytes is null)
        {
            return new SecretsSnapshot(path, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null, []);
        }

        string text = Encoding.UTF8.GetString(StripBom(bytes));
        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                values = _flattener.Flatten(text, section: null, path).Values;
            }
            catch (ImportException ex)
            {
                throw new ImportException(ImportErrorKind.IoFailure, $"The existing secrets file cannot be used: {ex.Message} Fix or remove it and try again.");
            }
        }

        return new SecretsSnapshot(path, values, ComputeHash(bytes), PermissionWarnings(path));
    }

    /// <param name="expectedHash">The <see cref="SecretsSnapshot.Hash"/> seen at analysis time (null: the file did not exist).</param>
    /// <exception cref="ImportException"><see cref="ImportErrorKind.ConcurrentModification"/> when the file changed since; <see cref="ImportErrorKind.IoFailure"/> on I/O errors.</exception>
    public SecretsWriteResult Write(string userSecretsId, IReadOnlyDictionary<string, string> values, string? expectedHash)
    {
        string path = GetSecretsPath(userSecretsId);
        byte[]? current = ReadBytes(path);
        string? currentHash = current is null ? null : ComputeHash(current);
        if (!string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ImportException(ImportErrorKind.ConcurrentModification, $"{path} changed after it was analyzed. Analyze again before importing.");
        }

        try
        {
            EnsureDirectory(Path.GetDirectoryName(path)!);
            string? backupPath = null;
            if (current is not null)
            {
                backupPath = path + ".bak";
                File.Copy(path, backupPath, overwrite: true);
                AtomicFile.CopyUnixMode(path, backupPath);
            }

            AtomicFile.Write(path, stream => WriteJson(stream, values), overwrite: true);
            return new SecretsWriteResult(path, backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(ImportErrorKind.IoFailure, $"Cannot write {path}: {ex.Message}", innerException: ex);
        }
    }

    /// <summary>Upper-case hex SHA-256, used to detect changes between Analyze and Apply.</summary>
    public static string ComputeHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static byte[]? ReadBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(ImportErrorKind.IoFailure, $"Cannot read {path}: {ex.Message}", innerException: ex);
        }
    }

    private static ReadOnlySpan<byte> StripBom(byte[] bytes) =>
        bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? bytes.AsSpan(Encoding.UTF8.Preamble.Length) : bytes;

    private static void WriteJson(Stream stream, IReadOnlyDictionary<string, string> values)
    {
        using Utf8JsonWriter writer = new(stream, WriterOptions);
        writer.WriteStartObject();
        foreach ((string key, string value) in values)
        {
            writer.WriteString(key, value);
        }

        writer.WriteEndObject();
    }

    private static void EnsureDirectory(string directory)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
        }
        else if (!Directory.Exists(directory))
        {
            // Only the folder we create ourselves gets 700; parents created along the way get the same.
            Directory.CreateDirectory(directory, DirectoryMode700);
        }
    }

    private static ImportWarning[] PermissionWarnings(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return [];
        }

        UnixFileMode mode = File.GetUnixFileMode(path);
        if ((mode & GroupOrOther) == 0)
        {
            return [];
        }

        string octal = Convert.ToString((int)mode & 0x1FF, 8);
        return [new ImportWarning(ImportWarningKind.InsecureFilePermissions, $"{path} has mode {octal}, so other users can access it. Consider 'chmod 600'; this tool does not change it.")];
    }
}