namespace SecretImport.Core;

/// <summary>
/// Atomic file replacement shared by <see cref="SecretsStore"/> and export (ADR-0002, ADR-0009, ADR-0015):
/// write a temp file in the same folder, then rename it over the target. On Unix the temp file is created with mode 600,
/// and when it replaces an existing file it first gets that file's mode, so a mode is never loosened or silently tightened.
/// </summary>
internal static class AtomicFile
{
    private const UnixFileMode FileMode600 = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    /// <param name="path">The full path of the target file. Its folder must exist.</param>
    /// <param name="write">Writes the content to the temp file stream.</param>
    /// <param name="overwrite">False makes the rename fail with an <see cref="IOException"/> when the target exists.</param>
    /// <exception cref="IOException">Or <see cref="UnauthorizedAccessException"/>: the write or rename failed (the temp file is removed).</exception>
    public static void Write(string path, Action<Stream> write, bool overwrite)
    {
        string tempPath = Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
            {
                // Never readable by others, not even for an instant (ADR-0009).
                options.UnixCreateMode = FileMode600;
            }

            using (FileStream stream = new(tempPath, options))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                CopyUnixMode(path, tempPath);
            }

            File.Move(tempPath, path, overwrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>Gives <paramref name="to"/> the Unix mode of <paramref name="from"/>; does nothing on Windows.</summary>
    public static void CopyUnixMode(string from, string to)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(to, File.GetUnixFileMode(from));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a stray temp file is harmless and never world-readable.
        }
    }
}
