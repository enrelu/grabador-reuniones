using System.Diagnostics;
using System.Globalization;

namespace GrabadorReuniones;

internal static class Program
{
    private const string MutexName = @"Local\GrabadorReuniones";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            MessageBox.Show(
                Textos.YaEnEjecucion,
                Textos.AppNombre,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var culture = new CultureInfo("es-ES");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            AppLog.Error("Excepción no controlada", e.ExceptionObject as Exception);
        };
        Application.ThreadException += (_, e) =>
        {
            AppLog.Error("Excepción en UI", e.Exception);
        };

        Application.Run(new TrayApplicationContext());
        GC.KeepAlive(mutex);
    }
}

internal static class AppLog
{
    private static readonly object Gate = new();
    private const long MaxBytes = 512 * 1024;

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var path = Core.AppPaths.LogFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            lock (Gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                {
                    var bak = path + ".1";
                    File.Copy(path, bak, overwrite: true);
                    File.Delete(path);
                }

                var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}";
                if (ex is not null)
                    line += " — " + ex;
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            Debug.WriteLine(message);
        }
    }
}
