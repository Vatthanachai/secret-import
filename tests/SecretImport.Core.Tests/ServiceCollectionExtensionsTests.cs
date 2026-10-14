using Microsoft.Extensions.DependencyInjection;

namespace SecretImport.Core.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSecretImportCore_ResolvesImportService_AsASingleton()
    {
        ServiceCollection services = new();

        IServiceCollection returned = services.AddSecretImportCore();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.Same(services, returned);
        IImportService service = provider.GetRequiredService<IImportService>();
        Assert.IsType<ImportService>(service);
        Assert.Same(service, provider.GetRequiredService<IImportService>());
        Assert.IsType<DotnetCli>(provider.GetRequiredService<IDotnetCli>());
        Assert.NotNull(provider.GetRequiredService<SecretsStore>());
    }

    [Fact]
    public void AddSecretImportCore_KeepsAnExistingImportServiceRegistration()
    {
        ServiceCollection services = new();
        FakeImportService fake = new();
        services.AddSingleton<IImportService>(fake);

        services.AddSecretImportCore();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(fake, provider.GetRequiredService<IImportService>());
    }

    [Fact]
    public void AddSecretImportCore_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ServiceCollectionExtensions.AddSecretImportCore(null!));
    }

    private sealed class FakeImportService : IImportService
    {
        public Task<DiscoveryResult> DiscoverProjectsAsync(string path, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ImportAnalysis> AnalyzeAsync(ImportRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task InitAsync(ProjectRef project, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ImportResult> ApplyAsync(ImportAnalysis analysis, CancellationToken ct = default) => throw new NotSupportedException();
    }
}