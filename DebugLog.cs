using System.IO;

namespace MuteMic;

// Lightweight timestamped logging so latency reports (e.g. "the border was late") can be
// diagnosed from real data instead of guessing. Never throws — logging must never be the
// thing that breaks muting.
internal static class DebugLog
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MuteMic", "debug.log");
    private static readonly object Lock = new();

    public static void Write(string message)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
        }
        catch { /* best-effort only */ }
    }
}
