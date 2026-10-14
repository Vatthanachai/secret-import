using Avalonia.Platform.Storage;

namespace SecretImport.Desktop.Services;

/// <summary>
/// <see cref="IFilePickerService"/> over Avalonia's <see cref="IStorageProvider"/> (normally <c>TopLevel.StorageProvider</c>
/// of the main window). The provider is resolved lazily on each call, because the window does not exist yet when this
/// service is constructed.
/// </summary>
public sealed class StorageProviderFilePicker(Func<IStorageProvider?> storageProvider) : IFilePickerService
{
    public static readonly FilePickerFileType ProjectFiles = new("Projects and solutions")
    {
        Patterns = ["*.csproj", "*.fsproj", "*.vbproj", "*.sln", "*.slnx"],
    };

    public static readonly FilePickerFileType JsonFiles = new("JSON files")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public Task<string?> PickProjectFileAsync()
    {
        return PickFileAsync("Select a project or solution", ProjectFiles);
    }

    public Task<string?> PickJsonFileAsync()
    {
        return PickFileAsync("Select the JSON file to import", JsonFiles);
    }

    public async Task<string?> PickFolderAsync()
    {
        IStorageProvider? provider = storageProvider();
        if (provider is null || !provider.CanPickFolder)
        {
            return null;
        }

        IReadOnlyList<IStorageFolder> folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a folder that contains projects",
            AllowMultiple = false,
        });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private async Task<string?> PickFileAsync(string title, FilePickerFileType fileType)
    {
        IStorageProvider? provider = storageProvider();
        if (provider is null || !provider.CanOpen)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [fileType, FilePickerFileTypes.All],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }
}
