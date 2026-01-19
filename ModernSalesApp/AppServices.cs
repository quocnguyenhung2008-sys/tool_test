using ModernSalesApp.Core;
using ModernSalesApp.Data;
using System.IO;

namespace ModernSalesApp;

public static class AppServices
{
    private static bool _initialized;

    static AppServices()
    {
        Core.AppPaths.EnsureDirectories();
    }

    public static ILogger Logger { get; } = new FileLogger(Path.Combine(Core.AppPaths.LogsDirectory, "app.log"));
    public static DbConnectionFactory DbFactory { get; } = new();

    public static Data.Repositories.PawnRepository Pawn { get; } = new(DbFactory, Logger);
    public static Data.Repositories.PawnCatalogRepository Catalog { get; } = new(DbFactory, Logger);

    public static async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        Core.AppPaths.EnsureDirectories();
        await SchemaInitializer.EnsureCreatedAsync(DbFactory, Logger);
        _initialized = true;
    }
}
