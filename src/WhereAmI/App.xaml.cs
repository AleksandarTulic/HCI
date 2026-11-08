using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WhereAmI;

public partial class App : Application
{
    /// <summary>Unexpected errors are written here, so a crash can be reported and investigated.</summary>
    public static string CrashLogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhereAmI", "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogCrash(args.ExceptionObject as Exception);
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            $"Something went wrong and the game has to close.\n\n{e.Exception.Message}\n\nDetails were saved to:\n{CrashLogPath}",
            "Where Am I?", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:u}] {exception}\n\n");
        }
        catch (IOException)
        {
            // Nothing more we can do.
        }
    }
}
