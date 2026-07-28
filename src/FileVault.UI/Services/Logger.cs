using System.IO;

namespace FileVault.UI.Services;

public static class Logger
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "filevault.log");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { }
    }

    public static void Log(string context, Exception ex)
    {
        Log($"ERROR in {context}: {ex}");
    }
}
