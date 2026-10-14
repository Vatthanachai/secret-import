namespace SecretImport.Desktop.Services;

/// <summary>
/// File and folder pickers used by the wizard. Each method returns the local path of the picked item,
/// or <see langword="null"/> when the user cancels or no picker is available.
/// </summary>
public interface IFilePickerService
{
    /// <summary>Picks a <c>.csproj</c>, <c>.fsproj</c>, <c>.vbproj</c>, <c>.sln</c> or <c>.slnx</c> file.</summary>
    Task<string?> PickProjectFileAsync();

    /// <summary>Picks a folder to search for projects.</summary>
    Task<string?> PickFolderAsync();

    /// <summary>Picks the JSON file to import.</summary>
    Task<string?> PickJsonFileAsync();
}
