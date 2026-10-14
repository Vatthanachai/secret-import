using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SecretImport.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IImportService"/> and the Core components it uses, all as singletons.
    /// Registrations use TryAdd, so a frontend (or test) that registered its own <see cref="IImportService"/> first keeps it.
    /// </summary>
    public static IServiceCollection AddSecretImportCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IDotnetCli>(static _ => new DotnetCli());
        services.TryAddSingleton(static _ => new SecretsStore());
        services.TryAddSingleton<ProjectLocator>();
        services.TryAddSingleton<UserSecretsIdResolver>();
        services.TryAddSingleton<SecretsInitializer>();
        services.TryAddSingleton<JsonFlattener>();
        services.TryAddSingleton<ImportPlanner>();
        services.TryAddSingleton<IImportService, ImportService>();
        return services;
    }
}