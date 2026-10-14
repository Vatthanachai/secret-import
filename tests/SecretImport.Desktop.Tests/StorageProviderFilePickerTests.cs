using System.Reflection;

using Avalonia.Platform.Storage;

using SecretImport.Desktop.Services;

namespace SecretImport.Desktop.Tests;

public class StorageProviderFilePickerTests
{
    private static readonly string LocalFile = Path.Combine(Path.GetTempPath(), "picked", "App.csproj");

    [Fact]
    public async Task WithoutAStorageProvider_EveryPickerReturnsNull()
    {
        StorageProviderFilePicker picker = new(static () => null);

        Assert.Null(await picker.PickProjectFileAsync());
        Assert.Null(await picker.PickJsonFileAsync());
        Assert.Null(await picker.PickFolderAsync());
    }

    [Fact]
    public async Task WhenPickersAreNotSupported_ReturnsNullWithoutOpeningOne()
    {
        FakeStorageProvider fake = FakeStorageProvider.Create(canOpen: false, canPickFolder: false);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        Assert.Null(await picker.PickProjectFileAsync());
        Assert.Null(await picker.PickFolderAsync());
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task PickProjectFile_FiltersProjectsAndSolutions()
    {
        FakeStorageProvider fake = FakeStorageProvider.Create(files: [LocalFile]);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        string? path = await picker.PickProjectFileAsync();

        Assert.Equal(LocalFile, path);
        FilePickerOpenOptions options = Assert.IsType<FilePickerOpenOptions>(Assert.Single(fake.Calls));
        Assert.False(options.AllowMultiple);
        Assert.Equal("Select a project or solution", options.Title);
        Assert.Equal([StorageProviderFilePicker.ProjectFiles, FilePickerFileTypes.All], options.FileTypeFilter);
        Assert.Equal(["*.csproj", "*.fsproj", "*.vbproj", "*.sln", "*.slnx"], StorageProviderFilePicker.ProjectFiles.Patterns);
    }

    [Fact]
    public async Task PickJsonFile_FiltersJson()
    {
        FakeStorageProvider fake = FakeStorageProvider.Create(files: [LocalFile]);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        Assert.Equal(LocalFile, await picker.PickJsonFileAsync());
        FilePickerOpenOptions options = Assert.IsType<FilePickerOpenOptions>(Assert.Single(fake.Calls));
        Assert.Equal([StorageProviderFilePicker.JsonFiles, FilePickerFileTypes.All], options.FileTypeFilter);
        Assert.Equal(["*.json"], StorageProviderFilePicker.JsonFiles.Patterns);
    }

    [Fact]
    public async Task CancelledFilePicker_ReturnsNull()
    {
        FakeStorageProvider fake = FakeStorageProvider.Create(files: []);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        Assert.Null(await picker.PickProjectFileAsync());
    }

    [Fact]
    public async Task PickFolder_ReturnsTheLocalPath()
    {
        string folder = Path.GetDirectoryName(LocalFile)!;
        FakeStorageProvider fake = FakeStorageProvider.Create(folders: [folder]);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        Assert.Equal(folder, await picker.PickFolderAsync());
        FolderPickerOpenOptions options = Assert.IsType<FolderPickerOpenOptions>(Assert.Single(fake.Calls));
        Assert.False(options.AllowMultiple);
    }

    [Fact]
    public async Task CancelledFolderPicker_ReturnsNull()
    {
        FakeStorageProvider fake = FakeStorageProvider.Create(folders: []);
        StorageProviderFilePicker picker = new(() => fake.Provider);

        Assert.Null(await picker.PickFolderAsync());
    }

    /// <summary>
    /// <see cref="IStorageProvider"/> fake built with <see cref="DispatchProxy"/>, so it only implements the members the picker uses.
    /// </summary>
    public class FakeStorageProvider : DispatchProxy
    {
        private bool _canOpen;
        private bool _canPickFolder;
        private string[] _files = [];
        private string[] _folders = [];

        public IStorageProvider Provider { get; private set; } = null!;

        public List<object> Calls { get; } = [];

        public static FakeStorageProvider Create(bool canOpen = true, bool canPickFolder = true, string[]? files = null, string[]? folders = null)
        {
            IStorageProvider provider = Create<IStorageProvider, FakeStorageProvider>();
            FakeStorageProvider fake = (FakeStorageProvider)(object)provider;
            fake._canOpen = canOpen;
            fake._canPickFolder = canPickFolder;
            fake._files = files ?? [];
            fake._folders = folders ?? [];
            fake.Provider = provider;
            return fake;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case "get_CanOpen":
                    return _canOpen;
                case "get_CanPickFolder":
                    return _canPickFolder;
                case nameof(IStorageProvider.OpenFilePickerAsync):
                    Calls.Add(args![0]!);
                    return Task.FromResult<IReadOnlyList<IStorageFile>>([.. _files.Select(static p => FakeItem.Create<IStorageFile>(p))]);
                case nameof(IStorageProvider.OpenFolderPickerAsync):
                    Calls.Add(args![0]!);
                    return Task.FromResult<IReadOnlyList<IStorageFolder>>([.. _folders.Select(static p => FakeItem.Create<IStorageFolder>(p))]);
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }

    /// <summary>A storage file or folder whose <see cref="IStorageItem.Path"/> is a local file URI.</summary>
    public class FakeItem : DispatchProxy
    {
        private string _path = string.Empty;

        public static T Create<T>(string path)
            where T : class, IStorageItem
        {
            T item = Create<T, FakeItem>();
            ((FakeItem)(object)item)._path = path;
            return item;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod!.Name switch
            {
                "get_Path" => new Uri(_path),
                "get_Name" => Path.GetFileName(_path),
                _ => throw new NotSupportedException(targetMethod.Name),
            };
        }
    }
}
