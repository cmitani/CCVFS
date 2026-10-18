using System;
using System.IO;
using System.Windows.Forms;

static void LogCrash(string text)
{
    try
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ccvfs_crash.txt");
        File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
    }
    catch { }
}

// Перехватчик исключений в UI-потоке (async void, обработчики событий)
Application.ThreadException += (_, e) =>
{
    LogCrash(e.Exception.ToString());
    MessageBox.Show(e.Exception.Message, "Необработанная ошибка",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
};

// Перехватчик исключений в других потоках
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    LogCrash(e.ExceptionObject.ToString());

Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.Run(new MainForm());
