using System.Reflection;
using System.IO;

namespace ModernSalesApp.Core;

public static class AppPaths
{
    public static string AppName => "ModernSalesApp";

    public static string AppDataDirectory
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, AppName);
        }
    }

    public static string LogsDirectory => Path.Combine(AppDataDirectory, "logs");

    public static string DatabasePath => Path.Combine(AppDataDirectory, "sales.sqlite");

    public static string EffectiveDatabasePath
    {
        get
        {
            var portable = Path.Combine(AppContext.BaseDirectory, "sales.sqlite");
            if (File.Exists(portable))
            {
                return portable;
            }

            var appData = DatabasePath;
            if (File.Exists(appData))
            {
                return appData;
            }

            try
            {
                Directory.CreateDirectory(AppContext.BaseDirectory);
                var probe = Path.Combine(AppContext.BaseDirectory, ".write_test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return portable;
            }
            catch
            {
                return appData;
            }
        }
    }

    public static string AppVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            return asm?.GetName().Version?.ToString() ?? "0.0.0";
        }
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
