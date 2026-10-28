using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

public static class Program
{
    private static void LogCrash(string? text)
    {
        try
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ccvfs_crash.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        // Перехватчик исключений в других потоках
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogCrash(e.ExceptionObject.ToString());

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogCrash(e.Exception.ToString());
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<CCVFS.App>()
            .UsePlatformDetect()
            .LogToTrace();
}
